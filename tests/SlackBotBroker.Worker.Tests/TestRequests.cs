using System.Text.Json;
using SlackBotBroker.Protocol.Payloads;

namespace SlackBotBroker.Worker.Tests;

internal static class TestRequests
{
    public static ExecutionRequestPayload EchoRequest(
        string executorKey = "mock",
        string operation = "echo",
        string? targetAlias = null,
        int? timeoutSeconds = null) => new()
    {
        RequestId = Guid.NewGuid(),
        CorrelationId = Guid.NewGuid(),
        SlackChannelId = "C1",
        SlackThreadTs = "1.1",
        RequestedByUserId = "U1",
        ExecutorKey = executorKey,
        Operation = operation,
        PayloadVersion = 1,
        Payload = JsonSerializer.SerializeToElement(new { }),
        CreatedAtUtc = DateTimeOffset.UtcNow,
        TargetAlias = targetAlias,
        TimeoutSeconds = timeoutSeconds,
    };
}
