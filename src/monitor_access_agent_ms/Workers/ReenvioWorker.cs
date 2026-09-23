using Microsoft.Extensions.Options;
using monitor_access_agent_ms.Configuration;
using monitor_access_agent_ms.Models;
using monitor_access_agent_ms.Services;

namespace monitor_access_agent_ms;

/// <summary>
/// Worker aislado que toma una orden del backend, localiza y valida el XML y
/// delega la operación local de reenvío. Nunca ejecuta dos órdenes a la vez.
/// </summary>
public sealed class ReenvioWorker(
    IReenvioApiClient apiClient,
    IArchivoXmlLocator xmlLocator,
    IReenvioDocumentoService reenvioService,
    IOptions<MonitorAgentOptions> monitorOptions,
    IOptions<ReenvioOptions> reenvioOptions,
    AgentRuntimeOptions runtimeOptions,
    IRunOnceCoordinator runOnceCoordinator,
    ILogger<ReenvioWorker> logger) : BackgroundService
{
    private readonly ReenvioOptions _options = reenvioOptions.Value;
    private readonly IReadOnlyList<PuntoOptions> _puntos = monitorOptions.Value.FuentesAccess
        .SelectMany(x => x.Puntos)
        .GroupBy(x => x.CodigoPunto, StringComparer.OrdinalIgnoreCase)
        .Select(x => x.First())
        .ToArray();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (!_options.Enabled)
            {
                logger.LogInformation("ReenvioWorker está deshabilitado por configuración.");
                return;
            }

            logger.LogInformation(
                "ReenvioWorker iniciado para {CantidadPuntos} punto(s), intervalo {Intervalo}s, modo {Modo}.",
                _puntos.Count, _options.IntervaloSegundos, _options.ModoOperacion);

            do
            {
                try
                {
                    await EjecutarCicloAsync(stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception,
                        "Falló el ciclo de reenvío; se intentará nuevamente en el siguiente intervalo.");
                }

                if (runtimeOptions.RunOnce)
                    break;

                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(_options.IntervaloSegundos), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
            while (!stoppingToken.IsCancellationRequested);
        }
        finally
        {
            runOnceCoordinator.CompletarWorker(nameof(ReenvioWorker));
        }
    }

    private async Task EjecutarCicloAsync(CancellationToken cancellationToken)
    {
        foreach (var punto in _puntos)
        {
            SolicitudReenvioResponse? solicitud;
            try
            {
                solicitud = await apiClient.TomarAsync(
                    punto.CodigoPunto, punto.ApiKey, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception,
                    "No fue posible consultar tareas de reenvío para {Punto}.", punto.CodigoPunto);
                continue;
            }

            if (solicitud is null)
                continue;

            // El worker es deliberadamente secuencial: después de tomar una
            // tarea no consulta otro punto hasta terminar y reportarla.
            await ProcesarSolicitudAsync(punto, solicitud, cancellationToken);
            return;
        }
    }

    private async Task ProcesarSolicitudAsync(
        PuntoOptions punto,
        SolicitudReenvioResponse solicitud,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Procesando solicitud de reenvío {Solicitud} para {Punto}, tipo {Tipo}.",
            solicitud.IdSolicitud, punto.CodigoPunto, solicitud.TipoComprobante);

        var archivoEncontrado = false;
        var reenviado = false;
        string? nombreArchivo = null;
        string mensaje;

        try
        {
            mensaje = ValidarSolicitud(punto, solicitud);
            if (mensaje.Length == 0)
            {
                var busqueda = await xmlLocator.BuscarAsync(solicitud, cancellationToken);
                archivoEncontrado = busqueda.ArchivoEncontrado;
                nombreArchivo = busqueda.NombreArchivo;
                mensaje = busqueda.Mensaje;

                if (!busqueda.EsAmbiguo && busqueda.RutaArchivo is not null &&
                    busqueda.NombreArchivo is not null)
                {
                    var operacion = await reenvioService.ReenviarAsync(
                        busqueda.RutaArchivo, busqueda.NombreArchivo, cancellationToken);
                    reenviado = operacion.Reenviado;
                    mensaje = operacion.Mensaje;
                    if (reenviado)
                        xmlLocator.InvalidarIndice();
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            mensaje = "Falló la operación local de reenvío: " + exception.Message;
            logger.LogError(exception,
                "Falló la ejecución local de la solicitud {Solicitud}.", solicitud.IdSolicitud);
        }

        var request = new ResultadoReenvioRequest(
            solicitud.TokenEjecucion,
            archivoEncontrado,
            reenviado,
            nombreArchivo,
            LimitarMensaje(mensaje));

        try
        {
            await apiClient.ReportarResultadoAsync(
                solicitud.IdSolicitud, request, punto.ApiKey, cancellationToken);
            logger.LogInformation(
                "Resultado de solicitud {Solicitud} reportado. Encontrado={Encontrado}, Reenviado={Reenviado}.",
                solicitud.IdSolicitud, archivoEncontrado, reenviado);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception,
                "No fue posible reportar el resultado de la solicitud {Solicitud}; " +
                "el backend deberá recuperarla por vencimiento del token.",
                solicitud.IdSolicitud);
        }
    }

    private static string ValidarSolicitud(PuntoOptions punto, SolicitudReenvioResponse solicitud)
    {
        if (solicitud.IdSolicitud <= 0)
            return "La tarea recibida no contiene un identificador válido.";
        if (solicitud.TokenEjecucion == Guid.Empty)
            return "La tarea recibida no contiene token de ejecución.";
        if (!string.Equals(
                solicitud.CodigoPunto, punto.CodigoPunto, StringComparison.OrdinalIgnoreCase))
            return "La tarea recibida pertenece a un punto diferente.";
        if (solicitud.IdEmisor != punto.IdEmisor)
            return "La tarea recibida pertenece a un emisor diferente.";
        if (!TiposDocumentoElectronico.EsTipoConocido(solicitud.TipoComprobante))
            return "La tarea contiene un tipo de comprobante no permitido.";
        if (solicitud.Serie.Length != 6 || !solicitud.Serie.All(char.IsDigit))
            return "La tarea contiene una serie inválida.";
        if (solicitud.Secuencial <= 0)
            return "La tarea contiene un secuencial inválido.";
        return string.Empty;
    }

    private static string LimitarMensaje(string mensaje) =>
        mensaje.Length <= 1000 ? mensaje : mensaje[..1000];
}
