using System.Net.Http.Headers;
using System.Net.Http.Json;
using monitor_access_agent_ms.Models;

namespace monitor_access_agent_ms.Services;

public sealed class MonitorApiClient(HttpClient httpClient) : IMonitorApiClient
{
    public async Task EnviarHeartbeatAsync(
        HeartbeatRequest request,
        string apiKey,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, string.Empty)
        {
            Content = JsonContent.Create(request)
        };

        if (!string.IsNullOrWhiteSpace(apiKey))
            message.Headers.Authorization = new AuthenticationHeaderValue("ApiKey", apiKey);

        using var response = await httpClient.SendAsync(message, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"La API respondió {(int)response.StatusCode} ({response.StatusCode}): {responseBody}");
    }
}
