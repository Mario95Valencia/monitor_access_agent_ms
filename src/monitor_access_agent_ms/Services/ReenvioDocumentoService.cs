using Microsoft.Extensions.Options;
using monitor_access_agent_ms.Configuration;
using monitor_access_agent_ms.Models;

namespace monitor_access_agent_ms.Services;

public sealed class ReenvioDocumentoService(IOptions<ReenvioOptions> options)
    : IReenvioDocumentoService
{
    private readonly ReenvioOptions _options = options.Value;

    public async Task<ResultadoOperacionReenvio> ReenviarAsync(
        string rutaXml,
        string nombreArchivo,
        CancellationToken cancellationToken)
    {
        if (_options.ModoOperacion == ReenvioOptions.ModoSoloValidar)
            return new ResultadoOperacionReenvio(false,
                "El XML fue validado, pero el mecanismo local de reenvío aún no está configurado.");

        if (_options.ModoOperacion != ReenvioOptions.ModoCopiarACarpeta)
            return new ResultadoOperacionReenvio(false,
                "El modo local de reenvío configurado no es válido.");

        if (!Directory.Exists(_options.RutaDestino))
            return new ResultadoOperacionReenvio(false,
                "La carpeta destino del proceso local no existe o no está disponible.");

        var nombreSeguro = Path.GetFileName(nombreArchivo);
        if (!string.Equals(nombreSeguro, nombreArchivo, StringComparison.Ordinal) ||
            !string.Equals(Path.GetExtension(nombreSeguro), ".xml", StringComparison.OrdinalIgnoreCase))
            return new ResultadoOperacionReenvio(false,
                "El nombre del XML no es seguro para copiarlo al destino.");

        var destino = Path.Combine(Path.GetFullPath(_options.RutaDestino), nombreSeguro);
        var temporal = Path.Combine(Path.GetFullPath(_options.RutaDestino),
            $".gap-tmp-{Guid.NewGuid():N}");
        if (File.Exists(destino))
            return new ResultadoOperacionReenvio(false,
                "Ya existe un archivo con el mismo nombre en la carpeta destino; no se sobrescribió.");

        try
        {
            await using var origen = new FileStream(
                rutaXml, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
                81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var copia = new FileStream(
                temporal, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous | FileOptions.WriteThrough);
            await origen.CopyToAsync(copia, cancellationToken);
            await copia.FlushAsync(cancellationToken);
            await copia.DisposeAsync();
            File.Move(temporal, destino, false);
            return new ResultadoOperacionReenvio(true,
                "El XML fue copiado correctamente a la carpeta consumida por el proceso local.");
        }
        catch
        {
            // Sólo se elimina una copia parcial creada por esta operación; el
            // XML original nunca se modifica ni se elimina.
            if (File.Exists(temporal))
                File.Delete(temporal);
            throw;
        }
    }
}
