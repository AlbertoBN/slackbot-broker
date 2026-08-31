using System.Text.Json;
using SlackBotBroker.Executors;

namespace SlackBotBroker.Executors.Tests;

public class MockExecutorOperationValidationTests
{
    [Fact]
    public async Task Unsupported_operation_is_rejected_before_the_configured_script_runs()
    {
        var executor = new MockExecutor
        {
            // If this ran, the test would hang forever without a timeout — proving the
            // operation check happens before any script-driven work is attempted.
            Script = new MockExecutorScript { Outcome = MockExecutorOutcome.Hang },
        };
        var context = new ExecutorMessageContext
        {
            RequestId = Guid.NewGuid(),
            Operation = "not-a-supported-operation",
            Payload = JsonSerializer.SerializeToElement(new { }),
        };

        var result = await executor.MessageAsync(context, progress: null, CancellationToken.None);

        Assert.Equal(ExecutorOutcomeStatus.Failure, result.Status);
        Assert.Contains("not-a-supported-operation", result.Summary);
    }
}
