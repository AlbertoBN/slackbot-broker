using System.Text.Json;
using SlackBotBroker.Broker;

namespace SlackBotBroker.IntegrationTests;

internal static class TestCommands
{
    public static SlackCommand Echo(
        string channelId = "C-IT",
        string threadTs = "1.1",
        string userId = IntegrationHarness.AuthorizedUser) => new()
    {
        UserId = userId,
        ChannelId = channelId,
        ThreadTs = threadTs,
        ExecutorKey = IntegrationHarness.ExecutorKey,
        Operation = IntegrationHarness.Operation,
        Payload = JsonSerializer.SerializeToElement(new { }),
    };
}
