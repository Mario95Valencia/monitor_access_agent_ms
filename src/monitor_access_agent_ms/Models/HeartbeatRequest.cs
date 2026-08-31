namespace monitor_access_agent_ms.Models;

public sealed record HeartbeatRequest(
    string CodigoPunto,
    long IdEmisor,
    string Serie,
    long? SecuencialEmitido,
    string? NumeroDocumento,
    DateTime? FechaDocumento,
    bool AccessDisponible,
    string VersionAgente,
    string? Error);
