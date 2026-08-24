using System.Text.Json;

namespace SlackBotBroker.Protocol.Payloads;

public sealed record ExecutorStatusEntry
{
    public required string ExecutorKey { get; init; }

    /// <summary>The executor's lifecycle state name (e.g. "Ready", "Busy"). Kept as a string so the protocol layer does not depend on the executor-framework project's lifecycle enum.</summary>
    public required string State { get; init; }

    public JsonElement? Capabilities { get; init; }
}
