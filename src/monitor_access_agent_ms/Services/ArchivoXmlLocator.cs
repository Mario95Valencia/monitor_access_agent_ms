using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using monitor_access_agent_ms.Configuration;
using monitor_access_agent_ms.Models;

namespace monitor_access_agent_ms.Services;

public sealed class ArchivoXmlLocator(
    IOptions<ReenvioOptions> options,
    ILogger<ArchivoXmlLocator> logger) : IArchivoXmlLocator
{
    private readonly ReenvioOptions _options = options.Value;
    private readonly SemaphoreSlim _indiceLock = new(1, 1);
    private IReadOnlyList<ArchivoIndexado> _indice = [];
    private DateTimeOffset _indiceExpira = DateTimeOffset.MinValue;

    public async Task<ResultadoBusquedaXml> BuscarAsync(
        SolicitudReenvioResponse solicitud,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_options.RutaParaEnviar))
            return new ResultadoBusquedaXml(false, false, null, null,
                "La carpeta configurada para buscar XML no existe o no está disponible.");

        var indice = await ObtenerIndiceAsync(false, cancellationToken);
        var candidatos = SeleccionarCandidatos(indice, solicitud);
        if (candidatos.Count == 0)
        {
            indice = await ObtenerIndiceAsync(true, cancellationToken);
            candidatos = SeleccionarCandidatos(indice, solicitud);
        }

        if (candidatos.Count == 0)
            return new ResultadoBusquedaXml(false, false, null, null,
                "No se encontró un XML candidato para la solicitud.");

        if (candidatos.Count > 100)
            return CrearAmbiguo(candidatos,
                "La búsqueda produjo demasiados candidatos para validarlos de forma segura.");

        var validos = new List<ArchivoIndexado>();
        foreach (var candidato in candidatos)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var identidad = await LeerIdentidadAsync(candidato.Ruta, cancellationToken);
                if (Coincide(identidad, solicitud))
                    validos.Add(candidato);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception,
                    "No fue posible validar uno de los XML candidatos para la solicitud {Solicitud}.",
                    solicitud.IdSolicitud);
            }
        }

        if (validos.Count == 1)
            return new ResultadoBusquedaXml(true, false, validos[0].Ruta, validos[0].Nombre,
                "XML localizado y validado contra la solicitud.");
        if (validos.Count > 1)
            return CrearAmbiguo(validos,
                "Más de un XML válido corresponde a la solicitud.");

        return new ResultadoBusquedaXml(true, false, null, null,
            "Se encontraron archivos candidatos, pero ninguno coincide exactamente con el XML solicitado.");
    }

    public void InvalidarIndice() => _indiceExpira = DateTimeOffset.MinValue;

    private async Task<IReadOnlyList<ArchivoIndexado>> ObtenerIndiceAsync(
        bool forzar,
        CancellationToken cancellationToken)
    {
        if (!forzar && DateTimeOffset.UtcNow < _indiceExpira)
            return _indice;

        await _indiceLock.WaitAsync(cancellationToken);
        try
        {
            if (!forzar && DateTimeOffset.UtcNow < _indiceExpira)
                return _indice;

            var rutaRaiz = Path.GetFullPath(_options.RutaParaEnviar);
            var opcion = _options.BuscarEnSubdirectorios
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;
            _indice = await Task.Run(() => Directory
                    .EnumerateFiles(rutaRaiz, "*.xml", opcion)
                    .Select(ruta => new ArchivoIndexado(
                        Path.GetFullPath(ruta), Path.GetFileName(ruta)))
                    .ToArray(),
                cancellationToken);
            _indiceExpira = DateTimeOffset.UtcNow.AddSeconds(_options.CacheSegundos);
            return _indice;
        }
        finally
        {
            _indiceLock.Release();
        }
    }

    private static IReadOnlyList<ArchivoIndexado> SeleccionarCandidatos(
        IReadOnlyList<ArchivoIndexado> indice,
        SolicitudReenvioResponse solicitud)
    {
        if (!string.IsNullOrWhiteSpace(solicitud.NombreArchivo))
        {
            var nombreSeguro = Path.GetFileName(solicitud.NombreArchivo);
            if (string.Equals(nombreSeguro, solicitud.NombreArchivo, StringComparison.Ordinal) &&
                string.Equals(Path.GetExtension(nombreSeguro), ".xml", StringComparison.OrdinalIgnoreCase))
            {
                var exactos = indice.Where(x => string.Equals(
                    x.Nombre, nombreSeguro, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (exactos.Length > 0)
                    return exactos;
            }
        }

        if (!string.IsNullOrWhiteSpace(solicitud.ClaveAcceso))
        {
            var porClave = indice.Where(x => x.Nombre.Contains(
                solicitud.ClaveAcceso, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (porClave.Length > 0)
                return porClave;
        }

        var identificador = solicitud.Serie + solicitud.Secuencial.ToString("D9");
        return indice.Where(x => SoloDigitos(x.Nombre).Contains(
            identificador, StringComparison.Ordinal)).ToArray();
    }

    private static async Task<IdentidadXml> LeerIdentidadAsync(
        string ruta,
        CancellationToken cancellationToken)
    {
        var settings = new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true
        };
        await using var stream = new FileStream(
            ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
            4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = XmlReader.Create(stream, settings);
        var documento = await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken);
        var documentos = new List<XDocument> { documento };

        foreach (var comprobante in documento.Descendants()
                     .Where(x => x.Name.LocalName.Equals("comprobante", StringComparison.OrdinalIgnoreCase)))
        {
            var contenido = comprobante.Value.Trim();
            if (!contenido.StartsWith('<'))
                continue;
            try
            {
                using var stringReader = new StringReader(contenido);
                using var innerReader = XmlReader.Create(stringReader, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null
                });
                documentos.Add(XDocument.Load(innerReader, LoadOptions.None));
            }
            catch (XmlException)
            {
                // El XML exterior todavía puede contener identidad suficiente.
            }
        }

        var claves = documentos.SelectMany(ExtraerClaves)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.Ordinal);
        var tipos = documentos.SelectMany(ExtraerTipos)
            .ToHashSet(StringComparer.Ordinal);
        var seriesSecuenciales = documentos.Select(ExtraerSerieSecuencial)
            .Where(x => x is not null)
            .Select(x => x!.Value)
            .ToHashSet();
        return new IdentidadXml(claves, tipos, seriesSecuenciales);
    }

    private static bool Coincide(IdentidadXml identidad, SolicitudReenvioResponse solicitud)
    {
        if (!identidad.Tipos.Contains(solicitud.TipoComprobante))
            return false;

        if (!string.IsNullOrWhiteSpace(solicitud.ClaveAcceso))
            return identidad.Claves.Contains(solicitud.ClaveAcceso);

        return identidad.SeriesSecuenciales.Contains((solicitud.Serie, solicitud.Secuencial));
    }

    private static IEnumerable<string> ExtraerClaves(XDocument documento) =>
        documento.Descendants()
            .Where(x => x.Name.LocalName is "claveAcceso" or "numeroAutorizacion")
            .Select(x => x.Value.Trim());

    private static IEnumerable<string> ExtraerTipos(XDocument documento)
    {
        var mapa = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["factura"] = "01",
            ["liquidacionCompra"] = "03",
            ["notaCredito"] = "04",
            ["notaDebito"] = "05",
            ["guiaRemision"] = "06",
            ["comprobanteRetencion"] = "07"
        };
        foreach (var element in documento.DescendantsAndSelf())
        {
            if (mapa.TryGetValue(element.Name.LocalName, out var tipo))
                yield return tipo;
            if (element.Name.LocalName.Equals("codDoc", StringComparison.OrdinalIgnoreCase))
            {
                var valor = element.Value.Trim();
                if (valor is "01" or "03" or "04" or "05" or "06" or "07")
                    yield return valor;
            }
        }
    }

    private static (string Serie, long Secuencial)? ExtraerSerieSecuencial(XDocument documento)
    {
        string? Valor(string nombre) => documento.Descendants()
            .FirstOrDefault(x => x.Name.LocalName.Equals(nombre, StringComparison.OrdinalIgnoreCase))
            ?.Value.Trim();

        var establecimiento = Valor("estab")?.PadLeft(3, '0');
        var puntoEmision = Valor("ptoEmi")?.PadLeft(3, '0');
        var secuencialTexto = Valor("secuencial");
        if (establecimiento?.Length != 3 || puntoEmision?.Length != 3 ||
            !long.TryParse(secuencialTexto, out var secuencial))
            return null;
        return (establecimiento + puntoEmision, secuencial);
    }

    private static ResultadoBusquedaXml CrearAmbiguo(
        IReadOnlyCollection<ArchivoIndexado> candidatos,
        string prefijo)
    {
        var resumen = string.Join(", ", candidatos.Take(5).Select(x => ResumirNombre(x.Nombre)));
        if (candidatos.Count > 5)
            resumen += $", y {candidatos.Count - 5} más";
        return new ResultadoBusquedaXml(true, true, null, null,
            $"{prefijo} Candidatos: {resumen}.");
    }

    private static string ResumirNombre(string nombre)
    {
        if (nombre.Length <= 24)
            return nombre;
        return nombre[..6] + "..." + nombre[^10..];
    }

    private static string SoloDigitos(string value) => new(value.Where(char.IsDigit).ToArray());

    private sealed record ArchivoIndexado(string Ruta, string Nombre);
    private sealed record IdentidadXml(
        HashSet<string> Claves,
        HashSet<string> Tipos,
        HashSet<(string Serie, long Secuencial)> SeriesSecuenciales);
}

internal static class XDocumentExtensions
{
    public static IEnumerable<XElement> DescendantsAndSelf(this XDocument document) =>
        document.Root is null ? [] : document.Root.DescendantsAndSelf();
}
