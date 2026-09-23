namespace monitor_access_agent_ms.Models;

public sealed record SolicitudReenvioResponse(
    long IdSolicitud,
    string CodigoPunto,
    long IdEmisor,
    string TipoComprobante,
    string Serie,
    long Secuencial,
    string? ClaveAcceso,
    string? NombreArchivo,
    string Estado,
    int CantidadIntentos,
    Guid TokenEjecucion,
    DateTime FechaSolicitud,
    string? DetalleResultado);

public sealed record ResultadoReenvioRequest(
    Guid TokenEjecucion,
    bool ArchivoEncontrado,
    bool Reenviado,
    string? NombreArchivo,
    string Mensaje);

public sealed record ApiResponseData<T>(T? Data);
