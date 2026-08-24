using System.Text;
using System.Text.Json;

namespace SlackBotBroker.Protocol.Framing;

/// <summary>Writes one compact JSON line per <see cref="MessageEnvelope"/> to an underlying <see cref="Stream"/>.</summary>
public sealed class NdjsonFrameWriter : IAsyncDisposable
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly StreamWriter _writer;

    public NdjsonFrameWriter(Stream stream, bool leaveOpen = true)
    {
        _writer = new StreamWriter(stream, Utf8NoBom, bufferSize: 4096, leaveOpen: leaveOpen)
        {
            NewLine = "\n",
            AutoFlush = false,
        };
    }

    public async Task WriteAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(envelope, ProtocolJsonContext.Default.MessageEnvelope);
        await _writer.WriteLineAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
        await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync() => await _writer.DisposeAsync().ConfigureAwait(false);
}
