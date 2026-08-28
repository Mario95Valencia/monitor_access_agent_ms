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
            fuente.Puntos.Count > 0 &&
            fuente.Puntos.All(punto =>
                !string.IsNullOrWhiteSpace(punto.CodigoPunto) &&
                punto.Serie.Length == 6 && punto.Serie.All(char.IsDigit) &&
                punto.Caja.Length == 3 && punto.Caja.All(char.IsDigit)));
}

public sealed class FuenteAccessOptions
{
    [Required] public string AccessPath { get; init; } = string.Empty;
    public string AccessPassword { get; init; } = string.Empty;
    public List<PuntoOptions> Puntos { get; init; } = [];
}

public sealed class PuntoOptions
{
    [Required] public string CodigoPunto { get; init; } = string.Empty;
    [Required] public string Serie { get; init; } = string.Empty;
    [Required] public string Caja { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
}

public sealed record AgentRuntimeOptions(bool RunOnce);
