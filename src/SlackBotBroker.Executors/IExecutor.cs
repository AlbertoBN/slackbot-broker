namespace SlackBotBroker.Executors;

/// <summary>A controlled adapter to one configured local application.</summary>
public interface IExecutor
{
    string ExecutorKey { get; }
    ExecutorCapabilities Capabilities { get; }
    ExecutorLifecycleState State { get; }

    Task<ExecutorConnectionResult> ConnectAsync(ExecutorConnectionContext context, CancellationToken cancellationToken);

    Task DisconnectAsync(ExecutorDisconnectContext context, CancellationToken cancellationToken);

    Task<ExecutorMessageResult> MessageAsync(
        ExecutorMessageContext context,
        IProgress<ExecutorProgress>? progress,
        CancellationToken cancellationToken);
}
