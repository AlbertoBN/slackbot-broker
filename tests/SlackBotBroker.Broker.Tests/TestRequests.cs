using System.Text.Json;
using SlackBotBroker.Protocol.Payloads;

namespace SlackBotBroker.Broker.Tests;

internal static class TestRequests
{
    public static ExecutionRequestPayload EchoRequest(string executorKey = "mock") => new()
    {
        RequestId = Guid.NewGuid(),
        CorrelationId = Guid.NewGuid(),
        SlackChannelId = "C1",
        SlackThreadTs = "1.1",
        RequestedByUserId = "U1",
        ExecutorKey = executorKey,
        Operation = "echo",
        PayloadVersion = 1,
        Payload = JsonSerializer.SerializeToElement(new { }),
        CreatedAtUtc = DateTimeOffset.UtcNow,
    };
}
