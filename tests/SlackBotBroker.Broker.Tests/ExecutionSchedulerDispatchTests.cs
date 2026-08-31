namespace SlackBotBroker.Broker.Tests;

public class ExecutionSchedulerDispatchTests
{
    [Fact]
    public async Task Two_admitted_requests_are_dispatched_in_FIFO_order()
    {
        var scheduler = new ExecutionScheduler(capacity: 2);
        var first = TestRequests.EchoRequest();
        var second = TestRequests.EchoRequest();
        Assert.True(scheduler.TryAdmit(first));
        Assert.True(scheduler.TryAdmit(second));

        var dispatchOrder = new List<Guid>();
        using var cts = new CancellationTokenSource();

        Task Dispatch(SlackBotBroker.Protocol.Payloads.ExecutionRequestPayload request, CancellationToken ct)
        {
            dispatchOrder.Add(request.RequestId);
            if (dispatchOrder.Count == 2)
            {
                cts.Cancel();
            }

            return Task.CompletedTask;
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scheduler.RunAsync(Dispatch, cts.Token));

        Assert.Equal([first.RequestId, second.RequestId], dispatchOrder);
    }

    [Fact]
    public async Task Second_request_is_not_dispatched_until_the_first_reaches_a_terminal_outcome()
    {
        var scheduler = new ExecutionScheduler(capacity: 2);
        var first = TestRequests.EchoRequest();
        var second = TestRequests.EchoRequest();
        Assert.True(scheduler.TryAdmit(first));
        Assert.True(scheduler.TryAdmit(second));

        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource();

        async Task Dispatch(SlackBotBroker.Protocol.Payloads.ExecutionRequestPayload request, CancellationToken ct)
        {
            if (request.RequestId == first.RequestId)
            {
                firstStarted.TrySetResult();
                await releaseFirst.Task;
            }
            else
            {
                secondStarted.TrySetResult();
                cts.Cancel();
            }
        }

        var runTask = scheduler.RunAsync(Dispatch, cts.Token);

        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(50);
        Assert.False(secondStarted.Task.IsCompleted);

        releaseFirst.TrySetResult();
        await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);
    }

    [Fact]
    public async Task Request_is_no_longer_active_once_its_terminal_outcome_is_delivered()
    {
        var scheduler = new ExecutionScheduler(capacity: 1);
        var request = TestRequests.EchoRequest();
        Assert.True(scheduler.TryAdmit(request));

        var dispatchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDispatch = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource();

        async Task Dispatch(SlackBotBroker.Protocol.Payloads.ExecutionRequestPayload r, CancellationToken ct)
        {
            dispatchStarted.TrySetResult();
            await releaseDispatch.Task;
        }

        Assert.False(scheduler.IsActive(request.RequestId));

        var runTask = scheduler.RunAsync(Dispatch, cts.Token);
        await dispatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(scheduler.IsActive(request.RequestId));

        releaseDispatch.TrySetResult();

        // Wait for the request to be removed from active state (RunAsync loops back to await
        // the channel afterward, so poll rather than depending on RunAsync itself returning).
        for (var i = 0; i < 100 && scheduler.IsActive(request.RequestId); i++)
        {
            await Task.Delay(5);
        }

        Assert.False(scheduler.IsActive(request.RequestId));

        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);
    }
}
