namespace SlackBotBroker.Broker.Tests;

public class SlackGatewayRedactionTests
{
    [Fact]
    public async Task Token_shaped_pattern_in_executor_output_is_redacted_before_delivery()
    {
        var slackClient = new FakeSlackClient();
        var scheduler = new ExecutionScheduler(capacity: 4);
        var policy = TestPolicies.Default(sensitivePatterns: [@"sk-[A-Za-z0-9]{8,}"]);
        var gateway = new SlackGateway(slackClient, scheduler, new FakeWorkerConnectionState(), policy);

        await gateway.HandleCommandAsync(TestPolicies.Command(channelId: "C-REDACT", threadTs: "1.1"), CancellationToken.None);
        var admitted = default(SlackBotBroker.Protocol.Payloads.ExecutionRequestPayload);
        using var cts = new CancellationTokenSource();
        var runTask = scheduler.RunAsync((r, ct) => { admitted = r; cts.Cancel(); return Task.CompletedTask; }, cts.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);

        await gateway.CompletedAsync(
            new SlackBotBroker.Protocol.Payloads.ExecutionCompletedPayload
            {
                RequestId = admitted!.RequestId,
                Summary = "found token sk-abcdef1234567890 in output",
                Duration = TimeSpan.FromSeconds(1),
                CompletedAtUtc = DateTimeOffset.UtcNow,
            },
            CancellationToken.None);

        var completionMessage = Assert.Single(slackClient.SentMessages, m => m.ChannelId == "C-REDACT" && m.ThreadTs == "1.1");
        Assert.DoesNotContain("sk-abcdef1234567890", completionMessage.Text);
        Assert.Contains("[REDACTED]", completionMessage.Text);
    }

    [Fact]
    public void Redactor_leaves_non_matching_text_untouched()
    {
        var redactor = new SensitiveContentRedactor([@"sk-[A-Za-z0-9]{8,}"]);

        var result = redactor.Redact("nothing sensitive here");

        Assert.Equal("nothing sensitive here", result);
    }
}
