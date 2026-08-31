using System.Text.Json;

namespace SlackBotBroker.Executors;

public sealed record ExecutorMessageContext
{
    public required Guid RequestId { get; init; }
    public required string Operation { get; init; }
    public required JsonElement Payload { get; init; }
    public string? TargetAlias { get; init; }
    public TimeSpan? Timeout { get; init; }
}
