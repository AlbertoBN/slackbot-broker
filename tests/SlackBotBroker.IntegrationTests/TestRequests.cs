using System.Text.Json;
using SlackBotBroker.Protocol.Payloads;

namespace SlackBotBroker.IntegrationTests;

internal static class TestRequests
{
    public static ExecutionRequestPayload Echo(string channelId = "C-FILLER") => new()
    {
        RequestId = Guid.NewGuid(),
        CorrelationId = Guid.NewGuid(),
        SlackChannelId = channelId,
        SlackThreadTs = "1.1",
        RequestedByUserId = IntegrationHarness.AuthorizedUser,
        ExecutorKey = IntegrationHarness.ExecutorKey,
        Operation = IntegrationHarness.Operation,
        PayloadVersion = 1,
        Payload = JsonSerializer.SerializeToElement(new { }),
        CreatedAtUtc = DateTimeOffset.UtcNow,
    };
}
