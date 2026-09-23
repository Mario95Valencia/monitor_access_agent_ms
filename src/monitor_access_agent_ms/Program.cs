using DotNetEnv;
using Microsoft.Extensions.Options;
using monitor_access_agent_ms;
using monitor_access_agent_ms.Configuration;
using monitor_access_agent_ms.Services;

var envPath = Path.Combine(AppContext.BaseDirectory, ".env");
if (File.Exists(envPath))
    Env.Load(envPath);

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "GAP Monitor Access Agent";
});

builder.Services
    .AddOptions<MonitorAgentOptions>()
    .Bind(builder.Configuration.GetSection(MonitorAgentOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => options.TieneFuentesValidas(),
        "Monitor:FuentesAccess debe contener al menos una fuente con ruta y puntos válidos; " +
        "cada serie debe tener seis dígitos, cada caja tres y al menos una fuente debe " +
        "soportar un tipo documental implementado.")
    .ValidateOnStart();

builder.Services
    .AddOptions<ReenvioOptions>()
    .Bind(builder.Configuration.GetSection(ReenvioOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => options.TieneConfiguracionValida(),
        "Reenvio contiene rutas, URL o modo de operación inválidos.")
    .ValidateOnStart();

builder.Services.AddSingleton<IAccessNotaReader, AccessNotaReader>();
builder.Services.AddHttpClient<IMonitorApiClient, MonitorApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<MonitorAgentOptions>>().Value;
    client.BaseAddress = new Uri(options.ApiUrl);
    client.Timeout = TimeSpan.FromSeconds(options.HttpTimeoutSeconds);
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    // Evita conservar indefinidamente una conexión creada antes de que el
    // backend se reinicie o cambie de dirección.
    PooledConnectionLifetime = TimeSpan.FromMinutes(2),
    PooledConnectionIdleTimeout = TimeSpan.FromSeconds(45),
    ConnectTimeout = TimeSpan.FromSeconds(10)
});
builder.Services.AddHttpClient<IReenvioApiClient, ReenvioApiClient>((serviceProvider, client) =>
{
    var monitor = serviceProvider.GetRequiredService<IOptions<MonitorAgentOptions>>().Value;
    var reenvio = serviceProvider.GetRequiredService<IOptions<ReenvioOptions>>().Value;
    client.BaseAddress = ResolverApiReenvio(monitor.ApiUrl, reenvio.ApiUrl);
    client.Timeout = TimeSpan.FromSeconds(monitor.HttpTimeoutSeconds);
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(2),
    PooledConnectionIdleTimeout = TimeSpan.FromSeconds(45),
    ConnectTimeout = TimeSpan.FromSeconds(10)
});
builder.Services.AddSingleton<IArchivoXmlLocator, ArchivoXmlLocator>();
builder.Services.AddSingleton<IReenvioDocumentoService, ReenvioDocumentoService>();
builder.Services.AddSingleton(new AgentRuntimeOptions(
    args.Any(x => string.Equals(x, "--once", StringComparison.OrdinalIgnoreCase))));
builder.Services.AddSingleton<IRunOnceCoordinator, RunOnceCoordinator>();
builder.Services.AddHostedService<MonitorWorker>();
builder.Services.AddHostedService<ReenvioWorker>();

await builder.Build().RunAsync();

static Uri ResolverApiReenvio(string monitorApiUrl, string reenvioApiUrl)
{
    if (!string.IsNullOrWhiteSpace(reenvioApiUrl))
        return AsegurarBarraFinal(new Uri(reenvioApiUrl, UriKind.Absolute));

    var heartbeat = new Uri(monitorApiUrl, UriKind.Absolute);
    const string sufijo = "/monitor/heartbeat";
    var path = heartbeat.AbsolutePath.EndsWith(sufijo, StringComparison.OrdinalIgnoreCase)
        ? heartbeat.AbsolutePath[..^sufijo.Length]
        : heartbeat.AbsolutePath.TrimEnd('/');
    var builder = new UriBuilder(heartbeat)
    {
        Path = path.TrimEnd('/') + "/",
        Query = string.Empty,
        Fragment = string.Empty
    };
    return builder.Uri;
}

static Uri AsegurarBarraFinal(Uri uri)
{
    if (uri.AbsoluteUri.EndsWith('/'))
        return uri;
    return new Uri(uri.AbsoluteUri + "/", UriKind.Absolute);
}
