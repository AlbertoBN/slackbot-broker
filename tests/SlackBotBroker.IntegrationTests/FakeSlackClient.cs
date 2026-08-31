using System.Threading.Channels;
using SlackBotBroker.Broker;

namespace SlackBotBroker.IntegrationTests;

/// <summary>Hand-written <see cref="ISlackClient"/> fake — no real Slack SDK involved, and no real Socket Mode connection needed for these tests.</summary>
public sealed class FakeSlackClient : ISlackClient
{
    private readonly Channel<SlackCommand> _incoming = Channel.CreateUnbounded<SlackCommand>();

    public List<(string ChannelId, string ThreadTs, string Text)> SentMessages { get; } = [];

    public void Enqueue(SlackCommand command) => _incoming.Writer.TryWrite(command);

    public IAsyncEnumerable<SlackCommand> ReceiveCommandsAsync(CancellationToken cancellationToken) =>
        _incoming.Reader.ReadAllAsync(cancellationToken);

    public Task SendMessageAsync(string channelId, string threadTs, string text, CancellationToken cancellationToken)
    {
        lock (SentMessages)
        {
            SentMessages.Add((channelId, threadTs, text));
        }

        return Task.CompletedTask;
    }
}
