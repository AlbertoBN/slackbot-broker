using System.Text;
using System.Text.Json;

namespace SlackBotBroker.Protocol.Framing;

/// <summary>
/// Reads one <see cref="MessageEnvelope"/> per line from an underlying <see cref="Stream"/>.
/// Each call reads exactly one frame, independent of how the underlying stream chunks its data.
/// </summary>
public sealed class NdjsonFrameReader : IAsyncDisposable
{
    private readonly StreamReader _reader;

    public NdjsonFrameReader(Stream stream, bool leaveOpen = true)
    {
        _reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: leaveOpen);
    }

    /// <summary>Reads the next frame, or <see langword="null"/> if the stream has ended.</summary>
    /// <exception cref="JsonException">The frame's JSON is malformed or missing a required envelope field.</exception>
    public async Task<MessageEnvelope?> ReadAsync(CancellationToken cancellationToken = default)
    {
        var line = await _reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (line is null)
        {
            return null;
        }

        return JsonSerializer.Deserialize(line, ProtocolJsonContext.Default.MessageEnvelope)
            ?? throw new JsonException("Envelope deserialized to null.");
    }

    public ValueTask DisposeAsync()
    {
        _reader.Dispose();
        return ValueTask.CompletedTask;
    }
}
