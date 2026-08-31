using System.Net.Sockets;
using SlackBotBroker.Protocol;
using SlackBotBroker.Protocol.Framing;
using SlackBotBroker.Protocol.Payloads;

namespace SlackBotBroker.Worker;

/// <summary>
/// Listens on a Unix domain socket, accepts the broker's connection, and routes incoming
/// protocol messages to <see cref="IExecutionDispatcher"/>. Accepts connections in a loop so a
/// broker reconnect after a dropped connection is handled without restarting the worker.
/// </summary>
public sealed class WorkerIpcServer(string socketPath, IExecutionDispatcher dispatcher)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(socketPath))
        {
            File.Delete(socketPath);
        }

        var directory = Path.GetDirectoryName(socketPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var listenSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listenSocket.Bind(new UnixDomainSocketEndPoint(socketPath));
        listenSocket.Listen(backlog: 1);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Socket connection;
                try
                {
                    connection = await listenSocket.AcceptAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                await HandleConnectionAsync(connection, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (File.Exists(socketPath))
            {
                File.Delete(socketPath);
            }
        }
    }

    private async Task HandleConnectionAsync(Socket connection, CancellationToken cancellationToken)
    {
        await using var stream = new NetworkStream(connection, ownsSocket: true);
        await using var reader = new NdjsonFrameReader(stream);
        await using var writer = new NdjsonFrameWriter(stream);
        var writeLock = new SemaphoreSlim(1, 1);
        var sink = new IpcExecutionEventSink(writer, writeLock);

        while (!cancellationToken.IsCancellationRequested)
        {
            MessageEnvelope? envelope;
            try
            {
                envelope = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                break; // connection broken or malformed frame; drop it and accept the next connection
            }

            if (envelope is null)
            {
                break; // peer closed the connection
            }

            await HandleEnvelopeAsync(envelope, writer, writeLock, sink, cancellationToken).ConfigureAwait(false);
        }
    }

    private Task HandleEnvelopeAsync(
        MessageEnvelope envelope,
        NdjsonFrameWriter writer,
        SemaphoreSlim writeLock,
        IExecutionEventSink sink,
        CancellationToken cancellationToken)
    {
        switch (envelope.MessageType)
        {
            case MessageType.HealthPing:
                return WriteLockedAsync(writer, writeLock, EnvelopeCodec.Create(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, new HealthPongPayload()), cancellationToken);

            case MessageType.ExecutionRequest:
                var request = envelope.GetExecutionRequestPayload();
                _ = Task.Run(() => dispatcher.DispatchAsync(request, sink, cancellationToken), cancellationToken);
                return Task.CompletedTask;

            case MessageType.CancelExecution:
                var cancel = envelope.GetCancelExecutionPayload();
                dispatcher.TryCancel(cancel.RequestId, cancel.Reason);
                return Task.CompletedTask;

            default:
                return Task.CompletedTask;
        }
    }

    private static async Task WriteLockedAsync(NdjsonFrameWriter writer, SemaphoreSlim writeLock, MessageEnvelope envelope, CancellationToken cancellationToken)
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
