using System.ComponentModel.DataAnnotations;

namespace monitor_access_agent_ms.Configuration;

public sealed class MonitorAgentOptions
{
    public const string SectionName = "Monitor";
    [Range(1, 1440)] public int IntervaloMinutos { get; init; } = 1;
    [Required, Url] public string ApiUrl { get; init; } = string.Empty;
    [Required] public string VersionAgente { get; init; } = "1.0.0";
    [Range(5, 300)] public int HttpTimeoutSeconds { get; init; } = 30;
    public List<FuenteAccessOptions> FuentesAccess { get; init; } = [];

    public bool TieneFuentesValidas() =>
        FuentesAccess.Count > 0 &&
        FuentesAccess.All(fuente =>
            !string.IsNullOrWhiteSpace(fuente.AccessPath) &&
            fuente.TiposDocumentoEfectivos.All(TiposDocumentoElectronico.EsTipoConocido) &&
            fuente.FallbackTiposDocumentoEfectivos.All(TiposDocumentoElectronico.EsTipoConocido) &&
            fuente.TieneConfiguracionGuiaValida() &&
            fuente.Tabla is "Nota" or "NotaDiaria" &&
            (string.IsNullOrWhiteSpace(fuente.FallbackAccessPath) ||
             fuente.FallbackTabla is "Nota" or "NotaDiaria") &&
            fuente.Puntos.Count > 0 &&
            fuente.Puntos.All(punto =>
                !string.IsNullOrWhiteSpace(punto.CodigoPunto) &&
                punto.IdEmisor > 0 &&
                punto.Serie.Length == 6 && punto.Serie.All(char.IsDigit) &&
                punto.Caja.Length == 3 && punto.Caja.All(char.IsDigit))) &&
        FuentesAccess.Any(fuente =>
            fuente.TiposDocumentoEfectivos.Any(TiposDocumentoElectronico.EstaImplementado) ||
            fuente.FallbackTiposDocumentoEfectivos.Any(TiposDocumentoElectronico.EstaImplementado));
}

public sealed class FuenteAccessOptions
{
    public string Codigo { get; init; } = string.Empty;
    [Required] public string AccessPath { get; init; } = string.Empty;
    public string AccessPassword { get; init; } = string.Empty;
    public string Tabla { get; init; } = "Nota";
    public string FallbackAccessPath { get; init; } = string.Empty;
    public string FallbackAccessPassword { get; init; } = string.Empty;
    public string FallbackTabla { get; init; } = "NotaDiaria";
    public List<string> TiposDocumento { get; init; } = [];
    public List<string> FallbackTiposDocumento { get; init; } = [];
    public string GuiaNumeroCampo { get; init; } = string.Empty;
    public string GuiaFechaCampo { get; init; } = string.Empty;
    public List<PuntoOptions> Puntos { get; init; } = [];

    private static readonly string[] TipoPredeterminado = [TiposDocumentoElectronico.Factura];
    public IReadOnlyCollection<string> TiposDocumentoEfectivos =>
        TiposDocumento.Count == 0 ? TipoPredeterminado : TiposDocumento;
    public IReadOnlyCollection<string> FallbackTiposDocumentoEfectivos =>
        FallbackTiposDocumento.Count == 0 ? TipoPredeterminado : FallbackTiposDocumento;

    public bool Soporta(string tipoDocumento) =>
        TiposDocumentoEfectivos.Contains(tipoDocumento, StringComparer.Ordinal);

    public bool FallbackSoporta(string tipoDocumento) =>
        FallbackTiposDocumentoEfectivos.Contains(tipoDocumento, StringComparer.Ordinal);

    public bool TieneConfiguracionGuiaValida()
    {
        var usaGuia = Soporta(TiposDocumentoElectronico.GuiaRemision) ||
                      FallbackSoporta(TiposDocumentoElectronico.GuiaRemision);
        if (!usaGuia)
            return true;

        return GuiaNumeroCampo is "numGuia" or "numFac" &&
               GuiaFechaCampo is "fechaEmisionDocSustento" or
                   "fechaIniTransporte" or "fechaFinTransporte";
    }
}

public sealed class PuntoOptions
{
    [Required] public string CodigoPunto { get; init; } = string.Empty;
    [Range(1, long.MaxValue)] public long IdEmisor { get; init; }
    [Required] public string Serie { get; init; } = string.Empty;
    [Required] public string Caja { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
}

public sealed record AgentRuntimeOptions(bool RunOnce);

public static class TiposDocumentoElectronico
{
    public const string Factura = "01";
    public const string LiquidacionCompra = "03";
    public const string NotaCredito = "04";
    public const string NotaDebito = "05";
    public const string GuiaRemision = "06";
    public const string Retencion = "07";

    private static readonly HashSet<string> Conocidos =
    [
        Factura,
        LiquidacionCompra,
        NotaCredito,
        NotaDebito,
        GuiaRemision,
        Retencion
    ];

    private static readonly HashSet<string> Implementados =
    [
        Factura,
        NotaCredito,
        NotaDebito,
        GuiaRemision
    ];

    public static bool EsTipoConocido(string tipoDocumento) => Conocidos.Contains(tipoDocumento);

    public static bool EstaImplementado(string tipoDocumento) => Implementados.Contains(tipoDocumento);
}
