namespace SlackBotBroker.Protocol.Payloads;

public sealed record ExecutionAcceptedPayload
{
    public required Guid RequestId { get; init; }
    public required DateTimeOffset AcceptedAtUtc { get; init; }
}
