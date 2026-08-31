using SlackBotBroker.Protocol.Payloads;

namespace SlackBotBroker.Broker.Tests;

public class SlackGatewayThreadRoutingTests
{
    [Fact]
    public async Task Progress_for_a_request_is_posted_to_its_originating_thread_and_no_other()
    {
        var slackClient = new FakeSlackClient();
        var scheduler = new ExecutionScheduler(capacity: 4);
        var gateway = new SlackGateway(slackClient, scheduler, new FakeWorkerConnectionState(), TestPolicies.Default());

        var command = TestPolicies.Command(channelId: "C-ORIGIN", threadTs: "999.001");
        await gateway.HandleCommandAsync(command, CancellationToken.None);

        // Drain the admitted request to learn its generated RequestId.
        var admittedRequest = default(ExecutionRequestPayload);
        using var cts = new CancellationTokenSource();
        var runTask = scheduler.RunAsync((r, ct) =>
        {
            admittedRequest = r;
            cts.Cancel();
            return Task.CompletedTask;
        }, cts.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);

        Assert.NotNull(admittedRequest);
        Assert.Equal("C-ORIGIN", admittedRequest.SlackChannelId);

        await gateway.ProgressAsync(
            new ExecutionProgressPayload { RequestId = admittedRequest.RequestId, Status = "Running", Message = "halfway", Timestamp = DateTimeOffset.UtcNow },
            CancellationToken.None);

        var progressMessage = Assert.Single(slackClient.SentMessages, m => m.Text == "halfway");
        Assert.Equal("C-ORIGIN", progressMessage.ChannelId);
        Assert.Equal("999.001", progressMessage.ThreadTs);
    }

    [Fact]
    public async Task Progress_for_an_unknown_request_id_is_not_posted_anywhere()
    {
        var slackClient = new FakeSlackClient();
        var scheduler = new ExecutionScheduler(capacity: 4);
        var gateway = new SlackGateway(slackClient, scheduler, new FakeWorkerConnectionState(), TestPolicies.Default());

        await gateway.ProgressAsync(
            new ExecutionProgressPayload { RequestId = Guid.NewGuid(), Status = "Running", Message = "orphaned", Timestamp = DateTimeOffset.UtcNow },
            CancellationToken.None);

        Assert.Empty(slackClient.SentMessages);
    }
}
