using SlackBotBroker.Executors;
using SlackBotBroker.Worker;

namespace SlackBotBroker.Worker.Tests;

public class ExecutionDispatcherSequentialExecutionTests
{
    /// <summary>Test double whose <see cref="MessageAsync"/> the test can hold open precisely, so it can assert exactly when a second dispatched request begins.</summary>
    private sealed class ControllableExecutor(string key) : IExecutor
    {
        public string ExecutorKey { get; } = key;
        public ExecutorCapabilities Capabilities { get; } = new() { SupportedOperations = ["echo"] };
        public ExecutorLifecycleState State { get; private set; } = ExecutorLifecycleState.Ready;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ExecutorConnectionResult> ConnectAsync(ExecutorConnectionContext context, CancellationToken cancellationToken) =>
            Task.FromResult(new ExecutorConnectionResult { Status = ExecutorConnectionStatus.Ready });

        public Task DisconnectAsync(ExecutorDisconnectContext context, CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task<ExecutorMessageResult> MessageAsync(ExecutorMessageContext context, IProgress<ExecutorProgress>? progress, CancellationToken cancellationToken)
        {
            State = ExecutorLifecycleState.Busy;
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            State = ExecutorLifecycleState.Ready;
            return new ExecutorMessageResult { Status = ExecutorOutcomeStatus.Success, Summary = "ok", Duration = TimeSpan.Zero };
        }
    }

    [Fact]
    public async Task Second_request_for_the_same_target_alias_does_not_start_until_the_first_finishes()
    {
        var executorA = new ControllableExecutor("a");
        var executorB = new ControllableExecutor("b");
        var registry = new ExecutorRegistry(
        [
            new ExecutorRegistration { Executor = executorA },
            new ExecutorRegistration { Executor = executorB },
        ]);
        var dispatcher = new ExecutionDispatcher(registry);

        var requestA = TestRequests.EchoRequest(executorKey: "a", targetAlias: "shared-repo");
        var requestB = TestRequests.EchoRequest(executorKey: "b", targetAlias: "shared-repo");

        var taskA = dispatcher.DispatchAsync(requestA, new RecordingExecutionEventSink(), CancellationToken.None);
        await executorA.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var taskB = dispatcher.DispatchAsync(requestB, new RecordingExecutionEventSink(), CancellationToken.None);

        // executorB must not have started yet: the shared target alias gate is held by executorA.
        await Task.Delay(50);
        Assert.False(executorB.Started.Task.IsCompleted);

        executorA.Release.TrySetResult();
        await taskA;

        await executorB.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        executorB.Release.TrySetResult();
        await taskB;
    }

    [Fact]
    public async Task Requests_without_a_shared_target_alias_are_not_serialized()
    {
        var executorA = new ControllableExecutor("a");
        var executorB = new ControllableExecutor("b");
        var registry = new ExecutorRegistry(
        [
            new ExecutorRegistration { Executor = executorA },
            new ExecutorRegistration { Executor = executorB },
        ]);
        var dispatcher = new ExecutionDispatcher(registry);

        var requestA = TestRequests.EchoRequest(executorKey: "a", targetAlias: "repo-a");
        var requestB = TestRequests.EchoRequest(executorKey: "b", targetAlias: "repo-b");

        var taskA = dispatcher.DispatchAsync(requestA, new RecordingExecutionEventSink(), CancellationToken.None);
        var taskB = dispatcher.DispatchAsync(requestB, new RecordingExecutionEventSink(), CancellationToken.None);

        await executorA.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await executorB.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        executorA.Release.TrySetResult();
        executorB.Release.TrySetResult();
        await Task.WhenAll(taskA, taskB);
    }
}
