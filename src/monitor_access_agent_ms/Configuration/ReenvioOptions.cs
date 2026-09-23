using System.ComponentModel.DataAnnotations;

namespace monitor_access_agent_ms.Configuration;

public sealed class ReenvioOptions
{
    public const string SectionName = "Reenvio";
    public const string ModoSoloValidar = "SoloValidar";
    public const string ModoCopiarACarpeta = "CopiarACarpeta";

    public bool Enabled { get; init; }
    [Required] public string RutaParaEnviar { get; init; } = @"C:\facturaElectronica\ParaEnviar";
    [Range(5, 3600)] public int IntervaloSegundos { get; init; } = 30;
    [Range(5, 3600)] public int CacheSegundos { get; init; } = 60;
    public bool BuscarEnSubdirectorios { get; init; }
    public string ApiUrl { get; init; } = string.Empty;
    public string ModoOperacion { get; init; } = ModoSoloValidar;
    public string RutaDestino { get; init; } = string.Empty;

    public bool TieneConfiguracionValida()
    {
        if (!Enabled)
            return true;
        if (!Path.IsPathFullyQualified(RutaParaEnviar))
            return false;
        if (!string.IsNullOrWhiteSpace(ApiUrl) &&
            (!Uri.TryCreate(ApiUrl, UriKind.Absolute, out var uri) ||
             uri.Scheme is not ("http" or "https")))
            return false;
        if (ModoOperacion == ModoSoloValidar)
            return true;
        return ModoOperacion == ModoCopiarACarpeta &&
               Path.IsPathFullyQualified(RutaDestino) &&
               !string.Equals(
                   Path.GetFullPath(RutaParaEnviar).TrimEnd(Path.DirectorySeparatorChar),
                   Path.GetFullPath(RutaDestino).TrimEnd(Path.DirectorySeparatorChar),
                   StringComparison.OrdinalIgnoreCase);
    }
}
