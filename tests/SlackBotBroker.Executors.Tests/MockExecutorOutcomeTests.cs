using System.Text.Json;
using SlackBotBroker.Executors;

namespace SlackBotBroker.Executors.Tests;

public class MockExecutorOutcomeTests
{
    private static ExecutorMessageContext EchoContext(TimeSpan? timeout = null) => new()
    {
        RequestId = Guid.NewGuid(),
        Operation = "echo",
        Payload = JsonSerializer.SerializeToElement(new { }),
        Timeout = timeout,
    };

    [Fact]
    public async Task Configured_to_succeed_returns_a_success_result()
    {
        var executor = new MockExecutor { Script = new MockExecutorScript { Outcome = MockExecutorOutcome.Succeed, SuccessSummary = "done" } };

        var result = await executor.MessageAsync(EchoContext(), progress: null, CancellationToken.None);

        Assert.Equal(ExecutorOutcomeStatus.Success, result.Status);
        Assert.Equal("done", result.Summary);
    }

    [Fact]
    public async Task Configured_to_fail_returns_a_structured_failure_without_any_external_call()
    {
        var executor = new MockExecutor { Script = new MockExecutorScript { Outcome = MockExecutorOutcome.Fail, FailureSummary = "boom" } };

        var result = await executor.MessageAsync(EchoContext(), progress: null, CancellationToken.None);

        Assert.Equal(ExecutorOutcomeStatus.Failure, result.Status);
        Assert.Equal("boom", result.Summary);
    }

    [Fact]
    public async Task Configured_progress_sequence_is_emitted_in_order_before_the_terminal_result()
    {
        var now = DateTimeOffset.UtcNow;
        var steps = new[]
        {
            new ExecutorProgress { Status = "Running", Message = "step 1", Timestamp = now },
            new ExecutorProgress { Status = "Running", Message = "step 2", Timestamp = now.AddSeconds(1) },
            new ExecutorProgress { Status = "Running", Message = "step 3", Timestamp = now.AddSeconds(2) },
        };
        var executor = new MockExecutor { Script = new MockExecutorScript { Outcome = MockExecutorOutcome.Succeed, ProgressSequence = steps } };

        var observed = new List<ExecutorProgress>();
        var progress = new Progress<ExecutorProgress>(observed.Add);

        var result = await executor.MessageAsync(EchoContext(), progress, CancellationToken.None);

        Assert.Equal(ExecutorOutcomeStatus.Success, result.Status);
        Assert.Equal(steps, observed);
    }

    [Fact]
    public async Task Hanging_operation_exceeding_its_timeout_returns_a_timeout_outcome()
    {
        var executor = new MockExecutor { Script = new MockExecutorScript { Outcome = MockExecutorOutcome.Hang } };

        var result = await executor.MessageAsync(EchoContext(TimeSpan.FromMilliseconds(20)), progress: null, CancellationToken.None);

        Assert.Equal(ExecutorOutcomeStatus.Timeout, result.Status);
    }

    [Fact]
    public async Task Hanging_operation_responds_to_caller_triggered_cancellation()
    {
        var executor = new MockExecutor { Script = new MockExecutorScript { Outcome = MockExecutorOutcome.Hang } };
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));

        var result = await executor.MessageAsync(EchoContext(), progress: null, cts.Token);

        Assert.Equal(ExecutorOutcomeStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task Executor_returns_to_ready_after_a_message_completes()
    {
        var executor = new MockExecutor { Script = new MockExecutorScript { Outcome = MockExecutorOutcome.Succeed } };
        await executor.ConnectAsync(new ExecutorConnectionContext { RequestId = Guid.NewGuid() }, CancellationToken.None);

        await executor.MessageAsync(EchoContext(), progress: null, CancellationToken.None);

        Assert.Equal(ExecutorLifecycleState.Ready, executor.State);
    }
}
