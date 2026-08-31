using System.Text.Json;

namespace SlackBotBroker.Broker.Tests;

internal static class TestPolicies
{
    public const string AuthorizedUser = "U-AUTHORIZED";
    public const string UnauthorizedUser = "U-UNKNOWN";

    public static SlackGatewayPolicy Default(IReadOnlyCollection<string>? sensitivePatterns = null) => new()
    {
        AuthorizedUserIds = [AuthorizedUser],
        Executors = new Dictionary<string, ExecutorPolicy>
        {
            ["mock"] = new ExecutorPolicy
            {
                AllowedOperations = ["echo", "apply"],
                HighImpactOperations = ["apply"],
            },
        },
        AllowedTargetAliases = ["repo-a"],
        SensitiveContentPatterns = sensitivePatterns ?? [],
    };

    public static SlackCommand Command(
        string userId = AuthorizedUser,
        string channelId = "C1",
        string threadTs = "100.001",
        string? executorKey = "mock",
        string? operation = "echo",
        string? targetAlias = null,
        Guid? confirmsRequestId = null) => new()
    {
        UserId = userId,
        ChannelId = channelId,
        ThreadTs = threadTs,
        ExecutorKey = executorKey,
        Operation = operation,
        TargetAlias = targetAlias,
        Payload = JsonSerializer.SerializeToElement(new { }),
        ConfirmsRequestId = confirmsRequestId,
    };
}
