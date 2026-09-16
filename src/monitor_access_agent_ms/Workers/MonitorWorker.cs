using Microsoft.Extensions.Options;
using monitor_access_agent_ms.Configuration;
using monitor_access_agent_ms.Models;
using monitor_access_agent_ms.Services;

namespace monitor_access_agent_ms;

/// <summary>
/// Worker dedicado exclusivamente a consultar las fuentes Access y publicar
/// el estado de monitoreo. No ejecuta ni atenderá órdenes de reenvío.
/// </summary>
public sealed class MonitorWorker(
    IAccessNotaReader accessReader,
    IMonitorApiClient apiClient,
    IOptions<MonitorAgentOptions> options,
    AgentRuntimeOptions runtimeOptions,
    IHostApplicationLifetime applicationLifetime,
    ILogger<MonitorWorker> logger) : BackgroundService
{
    private readonly MonitorAgentOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var fuente in _options.FuentesAccess)
        {
            var pendientes = fuente.TiposDocumento
                .Where(tipo => tipo != TiposDocumentoElectronico.Factura)
                .ToArray();
            if (pendientes.Length > 0)
                logger.LogWarning(
                    "Fuente {Fuente}: los tipos {Tipos} están configurados pero sus lectores aún no están habilitados.",
                    DescribirFuente(fuente), string.Join(", ", pendientes));
        }

        logger.LogInformation("Agente iniciado para {Punto}, caja {Caja}, serie {Serie}.",
            string.Join(", ", _options.FuentesAccess.SelectMany(x => x.Puntos).Select(x => x.CodigoPunto)),
            string.Join(", ", _options.FuentesAccess.SelectMany(x => x.Puntos).Select(x => x.Caja)),
            string.Join(", ", _options.FuentesAccess.SelectMany(x => x.Puntos).Select(x => x.Serie)));

        do
        {
            var inicio = DateTimeOffset.UtcNow;
            try
            {
                await EjecutarCicloAsync(stoppingToken);
                logger.LogInformation("Ciclo de monitoreo completado en {DuracionMs} ms.",
                    (DateTimeOffset.UtcNow - inicio).TotalMilliseconds);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Una falla no prevista en un ciclo no debe finalizar el servicio.
                logger.LogError(exception,
                    "El ciclo de monitoreo falló; se volverá a intentar en el siguiente intervalo.");
            }

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
        // Las fuentes son independientes. Una ruta lenta o no disponible no
        // debe impedir que las demás estaciones terminen su ciclo.
        await Task.WhenAll(_options.FuentesAccess
            .Where(fuente => fuente.Soporta(TiposDocumentoElectronico.Factura))
            .Select(
            fuente => ProcesarFuenteAsync(fuente, cancellationToken)));
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

    private static string DescribirFuente(FuenteAccessOptions fuente) =>
        string.IsNullOrWhiteSpace(fuente.Codigo) ? fuente.AccessPath : fuente.Codigo;

    private static string DescribirError(Exception exception)
    {
        var detalle = exception.InnerException is null
            ? exception.Message
            : $"{exception.Message} Detalle: {exception.GetBaseException().Message}";
        return LimitarError(detalle);
    }
}
