using monitor_access_agent_ms.Configuration;
using monitor_access_agent_ms.Models;

namespace monitor_access_agent_ms.Services;

public interface IAccessNotaReader
{
    Task<IReadOnlyList<ResultadoLecturaPunto>> ObtenerUltimasNotasAsync(
        FuenteAccessOptions fuente,
        CancellationToken cancellationToken);
}
