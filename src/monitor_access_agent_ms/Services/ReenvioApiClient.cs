using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using monitor_access_agent_ms.Models;

namespace monitor_access_agent_ms.Services;

public sealed class ReenvioApiClient(HttpClient httpClient) : IReenvioApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<SolicitudReenvioResponse?> TomarAsync(
        string codigoPunto,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var ruta = $"monitor/agentes/{Uri.EscapeDataString(codigoPunto)}/reenvios/tomar";
        using var message = CrearMensaje(HttpMethod.Post, ruta, apiKey);
        using var response = await httpClient.SendAsync(message, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NoContent)
            return null;

        var contenido = await response.Content.ReadAsStringAsync(cancellationToken);
        AsegurarExito(response, contenido);
        if (string.IsNullOrWhiteSpace(contenido))
            return null;

        var envelope = JsonSerializer.Deserialize<ApiResponseData<SolicitudReenvioResponse>>(
            contenido, JsonOptions);
        return envelope?.Data;
    }

    public async Task ReportarResultadoAsync(
        long idSolicitud,
        ResultadoReenvioRequest request,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var ruta = $"monitor/reenvios/{idSolicitud}/resultado";
        using var message = CrearMensaje(HttpMethod.Post, ruta, apiKey);
        message.Content = JsonContent.Create(request, options: JsonOptions);
        using var response = await httpClient.SendAsync(message, cancellationToken);
        var contenido = await response.Content.ReadAsStringAsync(cancellationToken);
        AsegurarExito(response, contenido);
    }

    private static HttpRequestMessage CrearMensaje(HttpMethod metodo, string ruta, string apiKey)
    {
        var message = new HttpRequestMessage(metodo, ruta);
        if (!string.IsNullOrWhiteSpace(apiKey))
            message.Headers.Authorization = new AuthenticationHeaderValue("ApiKey", apiKey);
        return message;
    }

    private static void AsegurarExito(HttpResponseMessage response, string contenido)
    {
        if (response.IsSuccessStatusCode)
            return;
        var detalle = contenido.Length <= 1000 ? contenido : contenido[..1000];
        throw new HttpRequestException(
            $"La API respondió {(int)response.StatusCode} ({response.StatusCode}): {detalle}",
            null, response.StatusCode);
    }
}
