namespace SlackBotBroker.Protocol.Payloads;

public sealed record DisconnectExecutorPayload
{
    public required string ExecutorKey { get; init; }
    public string? Reason { get; init; }
}
