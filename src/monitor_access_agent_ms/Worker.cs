using Microsoft.Extensions.Options;
using monitor_access_agent_ms.Configuration;
using monitor_access_agent_ms.Models;
using monitor_access_agent_ms.Services;

namespace monitor_access_agent_ms;

public sealed class Worker(
    IAccessNotaReader accessReader,
    IMonitorApiClient apiClient,
    IOptions<MonitorAgentOptions> options,
    AgentRuntimeOptions runtimeOptions,
    IHostApplicationLifetime applicationLifetime,
    ILogger<Worker> logger) : BackgroundService
{
    private readonly MonitorAgentOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Agente iniciado para {Punto}, caja {Caja}, serie {Serie}.",
            string.Join(", ", _options.FuentesAccess.SelectMany(x => x.Puntos).Select(x => x.CodigoPunto)),
            string.Join(", ", _options.FuentesAccess.SelectMany(x => x.Puntos).Select(x => x.Caja)),
            string.Join(", ", _options.FuentesAccess.SelectMany(x => x.Puntos).Select(x => x.Serie)));

        do
        {
            await EjecutarCicloAsync(stoppingToken);
            if (runtimeOptions.RunOnce)
            {
                applicationLifetime.StopApplication();
                break;
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(_options.IntervaloMinutos), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
        while (!stoppingToken.IsCancellationRequested);
    }

    private async Task EjecutarCicloAsync(CancellationToken cancellationToken)
    {
        foreach (var fuente in _options.FuentesAccess)
        {
            await ProcesarFuenteAsync(fuente, cancellationToken);
        }
    }

    private async Task ProcesarFuenteAsync(
        FuenteAccessOptions fuente,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ResultadoLecturaPunto> resultados;
        try
        {
            resultados = await accessReader.ObtenerUltimasNotasAsync(fuente, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "No fue posible abrir la fuente Access {AccessPath}.", fuente.AccessPath);
            resultados = fuente.Puntos
                .Select(punto => new ResultadoLecturaPunto(punto, null, exception))
                .ToArray();
        }

        foreach (var resultado in resultados)
        {
            await ProcesarResultadoAsync(resultado, cancellationToken);
        }
    }

    private async Task ProcesarResultadoAsync(
        ResultadoLecturaPunto resultado,
        CancellationToken cancellationToken)
    {
        var punto = resultado.Punto;
        HeartbeatRequest request;

        if (resultado.Nota is not null)
        {
            var nota = resultado.Nota;
            var accessDisponible = resultado.Error is null;
            request = new HeartbeatRequest(punto.CodigoPunto, punto.IdEmisor, punto.Serie, nota.Secuencial,
                nota.NumeroDocumento, nota.FechaDocumento, accessDisponible, _options.VersionAgente,
                resultado.Error is null ? null : DescribirError(resultado.Error));
            if (accessDisponible)
                logger.LogInformation(
                    "Punto {Punto}: Access disponible. Documento {Documento}, secuencial {Secuencial}.",
                    punto.CodigoPunto, nota.NumeroDocumento, nota.Secuencial);
            else
                logger.LogError(resultado.Error,
                    "Punto {Punto}: se obtuvo el documento {Documento}, pero falló la base crítica Nota.",
                    punto.CodigoPunto, nota.NumeroDocumento);
        }
        else
        {
            var error = resultado.Error ?? new InvalidOperationException("Error de lectura no especificado.");
            logger.LogError(error, "Punto {Punto}: no fue posible consultar Access.", punto.CodigoPunto);
            request = new HeartbeatRequest(punto.CodigoPunto, punto.IdEmisor, punto.Serie, null, null, null,
                false, _options.VersionAgente, DescribirError(error));
        }

        try
        {
            await apiClient.EnviarHeartbeatAsync(request, punto.ApiKey, cancellationToken);
            logger.LogInformation("Punto {Punto}: heartbeat enviado correctamente.", punto.CodigoPunto);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception,
                "Punto {Punto}: no fue posible enviar el heartbeat; se reintentará en el siguiente ciclo.",
                punto.CodigoPunto);
        }
    }

    private static string LimitarError(string value) => value.Length <= 1000 ? value : value[..1000];

    private static string DescribirError(Exception exception)
    {
        var detalle = exception.InnerException is null
            ? exception.Message
            : $"{exception.Message} Detalle: {exception.GetBaseException().Message}";
        return LimitarError(detalle);
    }
}
