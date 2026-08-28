using monitor_access_agent_ms.Models;

namespace monitor_access_agent_ms.Services;

public interface IMonitorApiClient
{
    Task EnviarHeartbeatAsync(
        HeartbeatRequest request,
        string apiKey,
        CancellationToken cancellationToken);
}
