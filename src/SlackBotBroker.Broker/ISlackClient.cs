namespace SlackBotBroker.Broker;

/// <summary>Narrow seam over the Slack Socket Mode connection: receiving commands and posting messages. Real Socket Mode wiring and a hand-written test fake both implement this; production code never depends on a specific Slack SDK type.</summary>
public interface ISlackClient
{
    IAsyncEnumerable<SlackCommand> ReceiveCommandsAsync(CancellationToken cancellationToken);

    Task SendMessageAsync(string channelId, string threadTs, string text, CancellationToken cancellationToken);
}
