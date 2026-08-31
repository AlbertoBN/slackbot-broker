using System.Text.Json;

namespace SlackBotBroker.Executors;

public enum ExecutorOutcomeStatus
{
    Success,
    Failure,
    Timeout,
    Cancelled,
}

public sealed record ExecutorMessageResult
{
    public required ExecutorOutcomeStatus Status { get; init; }
    public required string Summary { get; init; }
    public string? Detail { get; init; }
    public JsonElement? StructuredOutput { get; init; }
    public IReadOnlyList<string>? Artifacts { get; init; }
    public string? ExitCodeOrStatus { get; init; }
    public required TimeSpan Duration { get; init; }
}
