using monitor_access_agent_ms.Models;

namespace monitor_access_agent_ms.Services;

public interface IReenvioApiClient
{
    Task<SolicitudReenvioResponse?> TomarAsync(
        string codigoPunto,
        string apiKey,
        CancellationToken cancellationToken);

    Task ReportarResultadoAsync(
        long idSolicitud,
        ResultadoReenvioRequest request,
        string apiKey,
        CancellationToken cancellationToken);
}
