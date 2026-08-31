using SlackBotBroker.Protocol;
using SlackBotBroker.Protocol.Framing;
using SlackBotBroker.Protocol.Payloads;

namespace SlackBotBroker.Worker;

/// <summary>Forwards dispatcher lifecycle events to the broker as protocol envelopes over one shared IPC connection, serializing concurrent writers with <paramref name="writeLock"/>.</summary>
public sealed class IpcExecutionEventSink(NdjsonFrameWriter writer, SemaphoreSlim writeLock) : IExecutionEventSink
{
    public Task AcceptedAsync(ExecutionAcceptedPayload accepted, CancellationToken cancellationToken) =>
        WriteAsync(EnvelopeCodec.Create(accepted.RequestId, accepted.RequestId, DateTimeOffset.UtcNow, accepted), cancellationToken);

    public Task ProgressAsync(ExecutionProgressPayload progress, CancellationToken cancellationToken) =>
        WriteAsync(EnvelopeCodec.Create(progress.RequestId, progress.RequestId, DateTimeOffset.UtcNow, progress), cancellationToken);

    public Task CompletedAsync(ExecutionCompletedPayload completed, CancellationToken cancellationToken) =>
        WriteAsync(EnvelopeCodec.Create(completed.RequestId, completed.RequestId, DateTimeOffset.UtcNow, completed), cancellationToken);

    public Task FailedAsync(ExecutionFailedPayload failed, CancellationToken cancellationToken) =>
        WriteAsync(EnvelopeCodec.Create(failed.RequestId, failed.RequestId, DateTimeOffset.UtcNow, failed), cancellationToken);

    public Task CancelledAsync(ExecutionCancelledPayload cancelled, CancellationToken cancellationToken) =>
        WriteAsync(EnvelopeCodec.Create(cancelled.RequestId, cancelled.RequestId, DateTimeOffset.UtcNow, cancelled), cancellationToken);

    private async Task WriteAsync(MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        await writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await writer.WriteAsync(envelope, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            writeLock.Release();
        }
    }
}
