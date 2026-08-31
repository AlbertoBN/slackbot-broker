using System.Diagnostics.CodeAnalysis;

namespace SlackBotBroker.Executors;

public sealed class ExecutorRegistry : IExecutorRegistry
{
    private readonly IReadOnlyDictionary<string, ExecutorRegistration> _registrations;

    public ExecutorRegistry(IEnumerable<ExecutorRegistration> registrations)
    {
        _registrations = registrations.ToDictionary(r => r.Executor.ExecutorKey);
    }

    public bool TryGet(string executorKey, [NotNullWhen(true)] out IExecutor? executor)
    {
        if (_registrations.TryGetValue(executorKey, out var registration) && registration.Enabled)
        {
            executor = registration.Executor;
            return true;
        }

        executor = null;
        return false;
    }
}
