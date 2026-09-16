using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
using monitor_access_agent_ms.Models;

namespace monitor_access_agent_ms.Services;

public sealed class MonitorApiClient(
    HttpClient httpClient,
    ILogger<MonitorApiClient> logger) : IMonitorApiClient
{
    private const int MaxIntentos = 3;

    public async Task EnviarHeartbeatAsync(
        HeartbeatRequest request,
        string apiKey,
        CancellationToken cancellationToken)
    {
        Exception? ultimoError = null;
        for (var intento = 1; intento <= MaxIntentos; intento++)
        {
            try
            {
                using var message = CrearMensaje(request, apiKey);
                using var response = await httpClient.SendAsync(message, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    if (intento > 1)
                        logger.LogInformation(
                            "Conexión con el backend recuperada en el intento {Intento} para {Punto} tipo {TipoDocumento}.",
                            intento, request.CodigoPunto, request.TipoDocumento);
                    return;
                }

                var error = new HttpRequestException(
                    $"La API respondió {(int)response.StatusCode} ({response.StatusCode}): " +
                    LimitarRespuesta(responseBody), null, response.StatusCode);
                if (!EsTransitorio(response.StatusCode) || intento == MaxIntentos)
                    throw error;

                ultimoError = error;
                logger.LogWarning(
                    "Backend temporalmente no disponible ({Estado}). Reintento {Siguiente}/{Total} para {Punto}.",
                    (int)response.StatusCode, intento + 1, MaxIntentos, request.CodigoPunto);
            }
            catch (Exception exception) when (
                EsErrorTransitorio(exception) && !cancellationToken.IsCancellationRequested)
            {
                ultimoError = exception;
                if (intento == MaxIntentos)
                    break;

                logger.LogWarning(exception,
                    "No fue posible conectar con el backend. Reintento {Siguiente}/{Total} para {Punto}.",
                    intento + 1, MaxIntentos, request.CodigoPunto);
            }

            await Task.Delay(EsperaAntesDeReintento(intento), cancellationToken);
        }

        throw new HttpRequestException(
            $"No fue posible enviar el heartbeat después de {MaxIntentos} intentos.", ultimoError);
    }

    private static HttpRequestMessage CrearMensaje(HeartbeatRequest request, string apiKey)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, string.Empty)
        {
            Content = JsonContent.Create(request)
        };
        if (!string.IsNullOrWhiteSpace(apiKey))
            message.Headers.Authorization = new AuthenticationHeaderValue("ApiKey", apiKey);
        return message;
    }

    private static bool EsTransitorio(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
        (int)statusCode >= 500;

    private static bool EsErrorTransitorio(Exception exception) =>
        exception is TaskCanceledException ||
        exception is HttpRequestException httpException &&
        (httpException.StatusCode is null || EsTransitorio(httpException.StatusCode.Value));

    private static TimeSpan EsperaAntesDeReintento(int intento) =>
        TimeSpan.FromSeconds(intento switch { 1 => 1, 2 => 3, _ => 7 });

    private static string LimitarRespuesta(string respuesta) =>
        respuesta.Length <= 1000 ? respuesta : respuesta[..1000];
}
