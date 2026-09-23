using monitor_access_agent_ms.Configuration;

namespace monitor_access_agent_ms.Services;

public interface IRunOnceCoordinator
{
    void CompletarWorker(string nombreWorker);
}

public sealed class RunOnceCoordinator(
    AgentRuntimeOptions runtimeOptions,
    IHostApplicationLifetime applicationLifetime,
    ILogger<RunOnceCoordinator> logger) : IRunOnceCoordinator
{
    private const int WorkersEsperados = 2;
    private int _workersCompletados;

    public void CompletarWorker(string nombreWorker)
    {
        if (!runtimeOptions.RunOnce)
            return;

        var completados = Interlocked.Increment(ref _workersCompletados);
        logger.LogDebug("Worker {Worker} completó --once ({Completados}/{Esperados}).",
            nombreWorker, completados, WorkersEsperados);
        if (completados >= WorkersEsperados)
            applicationLifetime.StopApplication();
    }
}
