namespace SlackBotBroker.Executors;

public enum MockExecutorOutcome
{
    /// <summary>Return a Success result once the configured progress sequence has been emitted.</summary>
    Succeed,

    /// <summary>Return a Failure result once the configured progress sequence has been emitted.</summary>
    Fail,

    /// <summary>Wait until the caller's cancellation token or the message's timeout fires, simulating a long-running or stuck operation.</summary>
    Hang,
}

/// <summary>Configures how <see cref="MockExecutor.MessageAsync"/> behaves for its next invocation.</summary>
public sealed record MockExecutorScript
{
    public MockExecutorOutcome Outcome { get; init; } = MockExecutorOutcome.Succeed;
    public IReadOnlyList<ExecutorProgress> ProgressSequence { get; init; } = [];
    public string SuccessSummary { get; init; } = "mock executor completed";
    public string FailureSummary { get; init; } = "mock executor configured to fail";
}
