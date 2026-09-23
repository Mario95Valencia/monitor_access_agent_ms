using monitor_access_agent_ms.Models;

namespace monitor_access_agent_ms.Services;

public interface IReenvioDocumentoService
{
    Task<ResultadoOperacionReenvio> ReenviarAsync(
        string rutaXml,
        string nombreArchivo,
        CancellationToken cancellationToken);
}
