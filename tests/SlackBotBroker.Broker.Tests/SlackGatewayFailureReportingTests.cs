namespace SlackBotBroker.Broker.Tests;

public class SlackGatewayFailureReportingTests
{
    [Fact]
    public async Task Queue_full_rejection_is_visible_and_distinct_from_worker_unavailability()
    {
        var slackClient = new FakeSlackClient();
        var scheduler = new ExecutionScheduler(capacity: 1);
        Assert.True(scheduler.TryAdmit(TestRequests.EchoRequest())); // fill the queue directly
        var gateway = new SlackGateway(slackClient, scheduler, new FakeWorkerConnectionState(), TestPolicies.Default());

        await gateway.HandleCommandAsync(TestPolicies.Command(), CancellationToken.None);

        var sent = Assert.Single(slackClient.SentMessages);
        Assert.Contains("busy", sent.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("worker", sent.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Worker_unavailability_is_visible_and_distinct_from_queue_full()
    {
        var slackClient = new FakeSlackClient();
        var scheduler = new ExecutionScheduler(capacity: 4);
        var workerConnection = new FakeWorkerConnectionState { IsConnected = false };
        var gateway = new SlackGateway(slackClient, scheduler, workerConnection, TestPolicies.Default());

        await gateway.HandleCommandAsync(TestPolicies.Command(), CancellationToken.None);

        var sent = Assert.Single(slackClient.SentMessages);
        Assert.Contains("worker", sent.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unavailable", sent.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("busy", sent.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Failed_execution_outcome_is_visible_in_the_originating_thread()
    {
        var slackClient = new FakeSlackClient();
        var scheduler = new ExecutionScheduler(capacity: 4);
        var gateway = new SlackGateway(slackClient, scheduler, new FakeWorkerConnectionState(), TestPolicies.Default());

        // Simulate the routing that would exist after a real admission (task 6.6 covers the full path).
        await gateway.HandleCommandAsync(TestPolicies.Command(channelId: "C-FAIL", threadTs: "1.1"), CancellationToken.None);
        var admitted = default(SlackBotBroker.Protocol.Payloads.ExecutionRequestPayload);
        using var cts = new CancellationTokenSource();
        var runTask = scheduler.RunAsync((r, ct) => { admitted = r; cts.Cancel(); return Task.CompletedTask; }, cts.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);

        await gateway.FailedAsync(
            new SlackBotBroker.Protocol.Payloads.ExecutionFailedPayload
            {
                RequestId = admitted!.RequestId,
                Summary = "executor timed out",
                Detail = "detail",
                FailureCategory = "Timeout",
                FailedAtUtc = DateTimeOffset.UtcNow,
            },
            CancellationToken.None);

        var failureMessage = Assert.Single(slackClient.SentMessages, m => m.Text.Contains("executor timed out"));
        Assert.Equal("C-FAIL", failureMessage.ChannelId);
    }
}
