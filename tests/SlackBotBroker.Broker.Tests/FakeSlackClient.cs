using System.Threading.Channels;

namespace SlackBotBroker.Broker.Tests;

/// <summary>Hand-written <see cref="ISlackClient"/> fake — no real Slack SDK involved. Feeds commands to the gateway and records every message the gateway sends back.</summary>
public sealed class FakeSlackClient : ISlackClient
{
    private readonly Channel<SlackCommand> _incoming = Channel.CreateUnbounded<SlackCommand>();

    public List<(string ChannelId, string ThreadTs, string Text)> SentMessages { get; } = [];

    public void Enqueue(SlackCommand command) => _incoming.Writer.TryWrite(command);

    public void CompleteCommands() => _incoming.Writer.TryComplete();

    public IAsyncEnumerable<SlackCommand> ReceiveCommandsAsync(CancellationToken cancellationToken) =>
        _incoming.Reader.ReadAllAsync(cancellationToken);

    public Task SendMessageAsync(string channelId, string threadTs, string text, CancellationToken cancellationToken)
    {
        SentMessages.Add((channelId, threadTs, text));
        return Task.CompletedTask;
    }
}
