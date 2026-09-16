using monitor_access_agent_ms.Configuration;

namespace monitor_access_agent_ms.Models;

public sealed record ResultadoLecturaPunto(
    PuntoOptions Punto,
    string TipoDocumento,
    UltimaNota? Nota,
    Exception? Error);
