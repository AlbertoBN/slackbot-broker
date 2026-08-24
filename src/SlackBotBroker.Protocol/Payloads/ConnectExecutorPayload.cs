namespace SlackBotBroker.Protocol.Payloads;

public sealed record ConnectExecutorPayload
{
    public required string ExecutorKey { get; init; }
}
