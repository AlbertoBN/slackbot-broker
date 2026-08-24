using System.Text.Json;

namespace SlackBotBroker.Protocol.Payloads;

public sealed record ExecutionCompletedPayload
{
    public required Guid RequestId { get; init; }
    public required string Summary { get; init; }
    public string? Detail { get; init; }
    public JsonElement? StructuredOutput { get; init; }
    public IReadOnlyList<string>? Artifacts { get; init; }
    public string? ExitCodeOrStatus { get; init; }
    public required TimeSpan Duration { get; init; }
    public required DateTimeOffset CompletedAtUtc { get; init; }
}
