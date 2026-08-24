namespace SlackBotBroker.Protocol.Ipc;

/// <summary>
/// Retries a transport-specific connect delegate with bounded exponential backoff until it
/// succeeds or cancellation is requested, so a broken IPC connection is treated as recoverable
/// rather than fatal. Transport-agnostic: the caller supplies how to actually connect (e.g. to a
/// Unix domain socket), this type only owns the retry/backoff behavior.
/// </summary>
public sealed class IpcReconnector(IpcReconnectPolicy policy)
{
    /// <summary>Raised before each retry delay, with the 1-based attempt number that just failed and the delay before the next attempt.</summary>
    public event Action<int, TimeSpan>? RetryScheduled;

    public async Task<Stream> ConnectWithRetryAsync(
        Func<CancellationToken, Task<Stream>> connect,
        CancellationToken cancellationToken = default)
    {
        var attempt = 0;
        var delay = policy.InitialDelay;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await connect(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                attempt++;
                RetryScheduled?.Invoke(attempt, delay);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                var nextDelayMs = Math.Min(delay.TotalMilliseconds * policy.BackoffMultiplier, policy.MaxDelay.TotalMilliseconds);
                delay = TimeSpan.FromMilliseconds(nextDelayMs);
            }
        }
    }
}
