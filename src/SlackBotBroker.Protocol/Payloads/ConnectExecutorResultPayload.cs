namespace SlackBotBroker.Protocol.Payloads;

public sealed record ConnectExecutorResultPayload
{
    public required string ExecutorKey { get; init; }
    public required bool Success { get; init; }
    public string? State { get; init; }
    public string? FailureReason { get; init; }
}
