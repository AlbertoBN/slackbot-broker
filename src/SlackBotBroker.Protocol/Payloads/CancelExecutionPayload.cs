namespace SlackBotBroker.Protocol.Payloads;

public sealed record CancelExecutionPayload
{
    public required Guid RequestId { get; init; }
    public string? Reason { get; init; }
}
