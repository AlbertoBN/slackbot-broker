using System.Text.RegularExpressions;

namespace SlackBotBroker.Broker.Tests;

public class SlackGatewayConfirmationTests
{
    [Fact]
    public async Task High_impact_operation_is_withheld_and_a_confirmation_is_prompted()
    {
        var slackClient = new FakeSlackClient();
        var scheduler = new ExecutionScheduler(capacity: 4);
        var gateway = new SlackGateway(slackClient, scheduler, new FakeWorkerConnectionState(), TestPolicies.Default());

        await gateway.HandleCommandAsync(TestPolicies.Command(operation: "apply"), CancellationToken.None);

        var prompt = Assert.Single(slackClient.SentMessages);
        Assert.Contains("high-impact", prompt.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("confirm", prompt.Text, StringComparison.OrdinalIgnoreCase);

        // Nothing was dispatched yet.
        Assert.True(scheduler.TryAdmit(TestRequests.EchoRequest()));
    }

    [Fact]
    public async Task Confirming_a_previously_prompted_high_impact_operation_dispatches_it()
    {
        var slackClient = new FakeSlackClient();
        var scheduler = new ExecutionScheduler(capacity: 4);
        var gateway = new SlackGateway(slackClient, scheduler, new FakeWorkerConnectionState(), TestPolicies.Default());

        await gateway.HandleCommandAsync(TestPolicies.Command(operation: "apply"), CancellationToken.None);
        var prompt = Assert.Single(slackClient.SentMessages);
        var match = Regex.Match(prompt.Text, @"confirmation id: ([0-9a-fA-F-]{36})");
        Assert.True(match.Success);
        var confirmationId = Guid.Parse(match.Groups[1].Value);

        await gateway.HandleCommandAsync(TestPolicies.Command(confirmsRequestId: confirmationId), CancellationToken.None);

        var admitted = default(SlackBotBroker.Protocol.Payloads.ExecutionRequestPayload);
        using var cts = new CancellationTokenSource();
        var runTask = scheduler.RunAsync((r, ct) => { admitted = r; cts.Cancel(); return Task.CompletedTask; }, cts.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);

        Assert.NotNull(admitted);
        Assert.Equal("apply", admitted.Operation);
        Assert.Equal(confirmationId, admitted.RequestId);
    }

    [Fact]
    public async Task Confirming_an_unknown_id_does_not_dispatch_anything()
    {
        var slackClient = new FakeSlackClient();
        var scheduler = new ExecutionScheduler(capacity: 1);
        var gateway = new SlackGateway(slackClient, scheduler, new FakeWorkerConnectionState(), TestPolicies.Default());

        await gateway.HandleCommandAsync(TestPolicies.Command(confirmsRequestId: Guid.NewGuid()), CancellationToken.None);

        var sent = Assert.Single(slackClient.SentMessages);
        Assert.Contains("No pending confirmation", sent.Text);
        Assert.True(scheduler.TryAdmit(TestRequests.EchoRequest()));
    }
}
