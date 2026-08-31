using System.Diagnostics.CodeAnalysis;

namespace SlackBotBroker.Executors;

public interface IExecutorRegistry
{
    bool TryGet(string executorKey, [NotNullWhen(true)] out IExecutor? executor);
}
