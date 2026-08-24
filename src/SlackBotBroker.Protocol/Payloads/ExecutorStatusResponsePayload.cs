namespace SlackBotBroker.Protocol.Payloads;

public sealed record ExecutorStatusResponsePayload
{
    public required IReadOnlyList<ExecutorStatusEntry> Executors { get; init; }
}
