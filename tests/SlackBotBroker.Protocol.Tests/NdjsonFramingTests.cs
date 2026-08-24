using System.Text;
using System.Text.Json;
using SlackBotBroker.Protocol;
using SlackBotBroker.Protocol.Framing;
using SlackBotBroker.Protocol.Payloads;

namespace SlackBotBroker.Protocol.Tests;

public class NdjsonFramingTests
{
    [Fact]
    public async Task Consecutive_messages_are_read_back_independently_and_in_order()
    {
        var first = EnvelopeCodec.Create(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, new HealthPingPayload());
        var second = EnvelopeCodec.Create(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, new CancelExecutionPayload { RequestId = Guid.NewGuid() });

        using var buffer = new MemoryStream();
        await using (var writer = new NdjsonFrameWriter(buffer, leaveOpen: true))
        {
            await writer.WriteAsync(first);
            await writer.WriteAsync(second);
        }

        buffer.Position = 0;
        await using var reader = new NdjsonFrameReader(buffer, leaveOpen: true);

        var readFirst = await reader.ReadAsync();
        var readSecond = await reader.ReadAsync();
        var readThird = await reader.ReadAsync();

        Assert.NotNull(readFirst);
        Assert.Equal(first.MessageType, readFirst.MessageType);
        Assert.Equal(first.RequestId, readFirst.RequestId);

        Assert.NotNull(readSecond);
        Assert.Equal(second.MessageType, readSecond.MessageType);
        Assert.Equal(second.RequestId, readSecond.RequestId);

        Assert.Null(readThird);
    }

    [Fact]
    public async Task Writer_never_emits_an_embedded_newline_inside_a_frame()
    {
        var envelope = EnvelopeCodec.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            new ExecutionCancelledPayload { RequestId = Guid.NewGuid(), Reason = "line one", CancelledAtUtc = DateTimeOffset.UtcNow });

        using var buffer = new MemoryStream();
        await using (var writer = new NdjsonFrameWriter(buffer, leaveOpen: true))
        {
            await writer.WriteAsync(envelope);
        }

        var text = Encoding.UTF8.GetString(buffer.ToArray());
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Single(lines);
    }

    [Fact]
    public async Task Reader_rejects_a_malformed_frame_without_returning_a_payload()
    {
        var missingCorrelationId = """
            {"messageType":"HealthPing","protocolVersion":1,"requestId":"11111111-1111-1111-1111-111111111111","sentAtUtc":"2026-01-01T00:00:00Z","payload":{}}

            """;

        using var buffer = new MemoryStream(Encoding.UTF8.GetBytes(missingCorrelationId));
        await using var reader = new NdjsonFrameReader(buffer, leaveOpen: true);

        await Assert.ThrowsAsync<JsonException>(() => reader.ReadAsync());
    }
}
