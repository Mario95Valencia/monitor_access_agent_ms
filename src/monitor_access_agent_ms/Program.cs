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

builder.Services.AddSingleton<IAccessNotaReader, AccessNotaReader>();
builder.Services.AddHttpClient<IMonitorApiClient, MonitorApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<MonitorAgentOptions>>().Value;
    client.BaseAddress = new Uri(options.ApiUrl);
    client.Timeout = TimeSpan.FromSeconds(options.HttpTimeoutSeconds);
});
builder.Services.AddSingleton(new AgentRuntimeOptions(
    args.Any(x => string.Equals(x, "--once", StringComparison.OrdinalIgnoreCase))));
// El agente se organiza en workers independientes. Por ahora se habilita
// solamente el monitoreo; ReenvioWorker se incorporará cuando su contrato
// con el backend y el mecanismo de reenvío estén definidos.
builder.Services.AddHostedService<MonitorWorker>();

await builder.Build().RunAsync();
