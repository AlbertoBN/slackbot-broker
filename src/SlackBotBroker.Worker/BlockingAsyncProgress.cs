namespace SlackBotBroker.Worker;

/// <summary>
/// Adapts an async handler to the synchronous <see cref="IProgress{T}"/> contract by blocking
/// each <see cref="Report"/> call until the handler completes. Executors call <c>Report</c>
/// synchronously in sequence (no awaiting between calls), so blocking here is what guarantees
/// progress events reach the sink strictly in order — <see cref="System.Progress{T}"/> instead
/// posts through a captured <see cref="SynchronizationContext"/>, which would not preserve order.
/// Safe in this worker process: no UI <see cref="SynchronizationContext"/> is ever captured here.
/// </summary>
internal sealed class BlockingAsyncProgress<T>(Func<T, Task> handler) : IProgress<T>
{
    public void Report(T value) => handler(value).GetAwaiter().GetResult();
}
