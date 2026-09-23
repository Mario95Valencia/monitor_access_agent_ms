namespace monitor_access_agent_ms.Models;

public sealed record ResultadoBusquedaXml(
    bool ArchivoEncontrado,
    bool EsAmbiguo,
    string? RutaArchivo,
    string? NombreArchivo,
    string Mensaje);

public sealed record ResultadoOperacionReenvio(bool Reenviado, string Mensaje);
