using SlackBotBroker.Protocol.Ipc;

namespace SlackBotBroker.Protocol.Tests;

public class IpcReconnectorTests
{
    [Fact]
    public async Task Retries_after_a_failed_connect_attempt_instead_of_propagating()
    {
        var policy = new IpcReconnectPolicy { InitialDelay = TimeSpan.FromMilliseconds(1), MaxDelay = TimeSpan.FromMilliseconds(5) };
        var reconnector = new IpcReconnector(policy);

        var attempts = 0;
        var retrySignals = new List<int>();
        reconnector.RetryScheduled += (attempt, _) => retrySignals.Add(attempt);

        Task<Stream> Connect(CancellationToken ct)
        {
            attempts++;
            if (attempts < 3)
            {
                throw new IOException("connection refused");
            }

            return Task.FromResult<Stream>(new MemoryStream());
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var stream = await reconnector.ConnectWithRetryAsync(Connect, cts.Token);

        Assert.NotNull(stream);
        Assert.Equal(3, attempts);
        Assert.Equal([1, 2], retrySignals);
    }

    [Fact]
    public async Task Stops_retrying_once_cancellation_is_requested()
    {
        var policy = new IpcReconnectPolicy { InitialDelay = TimeSpan.FromMilliseconds(50) };
        var reconnector = new IpcReconnector(policy);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var connectAttempted = false;
        Task<Stream> AlwaysFails(CancellationToken ct)
        {
            connectAttempted = true;
            throw new IOException("connection refused");
        }

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            reconnector.ConnectWithRetryAsync(AlwaysFails, cts.Token));

        Assert.False(connectAttempted, "cancellation before the first attempt must prevent any connect call");
    }

    [Fact]
    public async Task Stops_retrying_when_cancellation_arrives_between_attempts()
    {
        var policy = new IpcReconnectPolicy { InitialDelay = TimeSpan.FromMilliseconds(200) };
        var reconnector = new IpcReconnector(policy);

        using var cts = new CancellationTokenSource();
        var attempts = 0;

        Task<Stream> AlwaysFails(CancellationToken ct)
        {
            attempts++;
            throw new IOException("connection refused");
        }

        reconnector.RetryScheduled += (_, _) => cts.Cancel();

        // Task.Delay surfaces cancellation as the more specific TaskCanceledException.
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            reconnector.ConnectWithRetryAsync(AlwaysFails, cts.Token));

        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task Backoff_delay_grows_up_to_the_configured_maximum()
    {
        var policy = new IpcReconnectPolicy
        {
            InitialDelay = TimeSpan.FromMilliseconds(1),
            MaxDelay = TimeSpan.FromMilliseconds(4),
            BackoffMultiplier = 2.0,
        };
        var reconnector = new IpcReconnector(policy);

        var delays = new List<TimeSpan>();
        reconnector.RetryScheduled += (_, delay) => delays.Add(delay);

        var attempts = 0;
        Task<Stream> Connect(CancellationToken ct)
        {
            attempts++;
            if (attempts < 5)
            {
                throw new IOException("connection refused");
            }

            return Task.FromResult<Stream>(new MemoryStream());
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await reconnector.ConnectWithRetryAsync(Connect, cts.Token);

        Assert.Equal(TimeSpan.FromMilliseconds(1), delays[0]);
        Assert.Equal(TimeSpan.FromMilliseconds(2), delays[1]);
        Assert.Equal(TimeSpan.FromMilliseconds(4), delays[2]);
        Assert.Equal(TimeSpan.FromMilliseconds(4), delays[3]); // capped at MaxDelay
    }
}
