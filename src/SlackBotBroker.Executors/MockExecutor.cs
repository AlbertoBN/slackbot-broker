using System.Diagnostics;

namespace SlackBotBroker.Executors;

/// <summary>
/// Reference <see cref="IExecutor"/> implementation with no real external process or service
/// behind it, configurable to simulate success, failure, timeout, cancellation, and multi-step
/// progress — so the broker/worker pipeline is usable and testable before any real
/// application-specific executor exists.
/// </summary>
public sealed class MockExecutor : IExecutor
{
    public string ExecutorKey { get; }

    public ExecutorCapabilities Capabilities { get; }

    public ExecutorLifecycleState State { get; private set; } = ExecutorLifecycleState.Disconnected;

    /// <summary>Configures the outcome of the next <see cref="MessageAsync"/> call.</summary>
    public MockExecutorScript Script { get; set; } = new();

    /// <summary>When true, <see cref="ConnectAsync"/> reports a failure instead of becoming Ready.</summary>
    public bool FailConnect { get; init; }

    /// <summary>When true, this executor simulates adapting an externally managed application: <see cref="DisconnectAsync"/> releases adapter resources only, never the simulated underlying process.</summary>
    public bool IsExternallyManaged { get; init; }

    /// <summary>Test-visible signal for whether <see cref="DisconnectAsync"/> stopped the simulated underlying process.</summary>
    public bool UnderlyingProcessStopped { get; private set; }

    public MockExecutor(string executorKey = "mock", ExecutorCapabilities? capabilities = null)
    {
        ExecutorKey = executorKey;
        Capabilities = capabilities ?? new ExecutorCapabilities
        {
            SupportedOperations = ["echo"],
            SupportsConnect = true,
            SupportsDisconnect = true,
            SupportsCancellation = true,
            SupportsProgress = true,
            HasManagedLifecycle = true,
        };
    }

    public Task<ExecutorConnectionResult> ConnectAsync(ExecutorConnectionContext context, CancellationToken cancellationToken)
    {
        if (State is ExecutorLifecycleState.Ready or ExecutorLifecycleState.Busy)
        {
            return Task.FromResult(new ExecutorConnectionResult { Status = ExecutorConnectionStatus.Ready });
        }

        if (FailConnect)
        {
            State = ExecutorLifecycleState.Faulted;
            return Task.FromResult(new ExecutorConnectionResult
            {
                Status = ExecutorConnectionStatus.Faulted,
                FailureReason = "mock executor configured to fail connect",
            });
        }

        State = ExecutorLifecycleState.Ready;
        return Task.FromResult(new ExecutorConnectionResult { Status = ExecutorConnectionStatus.Ready });
    }

    public Task DisconnectAsync(ExecutorDisconnectContext context, CancellationToken cancellationToken)
    {
        if (!IsExternallyManaged)
        {
            UnderlyingProcessStopped = true;
        }

        State = ExecutorLifecycleState.Disconnected;
        return Task.CompletedTask;
    }

    public async Task<ExecutorMessageResult> MessageAsync(
        ExecutorMessageContext context,
        IProgress<ExecutorProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!Capabilities.SupportedOperations.Contains(context.Operation))
        {
            return new ExecutorMessageResult
            {
                Status = ExecutorOutcomeStatus.Failure,
                Summary = $"operation '{context.Operation}' is not supported by executor '{ExecutorKey}'",
                Duration = TimeSpan.Zero,
            };
        }

        var stopwatch = Stopwatch.StartNew();
        State = ExecutorLifecycleState.Busy;

        using var timeoutCts = context.Timeout is { } timeout ? new CancellationTokenSource(timeout) : null;
        using var linkedCts = timeoutCts is null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            if (Capabilities.SupportsProgress)
            {
                foreach (var step in Script.ProgressSequence)
                {
                    linkedCts.Token.ThrowIfCancellationRequested();
                    progress?.Report(step);
                }
            }

            if (Script.Outcome == MockExecutorOutcome.Hang)
            {
                // Only completes via cancellation (caller-triggered or timeout), handled below.
                await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, linkedCts.Token).ConfigureAwait(false);
            }

            return Script.Outcome == MockExecutorOutcome.Fail
                ? new ExecutorMessageResult { Status = ExecutorOutcomeStatus.Failure, Summary = Script.FailureSummary, Duration = stopwatch.Elapsed }
                : new ExecutorMessageResult { Status = ExecutorOutcomeStatus.Success, Summary = Script.SuccessSummary, Duration = stopwatch.Elapsed };
        }
        catch (OperationCanceledException)
        {
            if (timeoutCts?.IsCancellationRequested == true)
            {
                return new ExecutorMessageResult
                {
                    Status = ExecutorOutcomeStatus.Timeout,
                    Summary = "operation exceeded its configured timeout",
                    Duration = stopwatch.Elapsed,
                };
            }

            return new ExecutorMessageResult
            {
                Status = ExecutorOutcomeStatus.Cancelled,
                Summary = "operation was cancelled",
                Duration = stopwatch.Elapsed,
            };
        }
        finally
        {
            if (State == ExecutorLifecycleState.Busy)
            {
                State = ExecutorLifecycleState.Ready;
            }
        }
    }
}
