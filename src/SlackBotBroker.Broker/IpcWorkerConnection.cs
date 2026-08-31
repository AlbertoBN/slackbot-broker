using System.Collections.Concurrent;
using System.Net.Sockets;
using SlackBotBroker.Protocol;
using SlackBotBroker.Protocol.Framing;
using SlackBotBroker.Protocol.Ipc;
using SlackBotBroker.Protocol.Payloads;

namespace SlackBotBroker.Broker;

/// <summary>
/// Maintains the broker's Unix domain socket connection to the worker, reconnecting with bounded
/// backoff when it breaks. Submits admitted requests (used as the <see cref="ExecutionScheduler"/>
/// dispatch delegate) and forwards every lifecycle event the worker reports to
/// <see cref="EventListener"/>.
/// </summary>
public sealed class IpcWorkerConnection(string socketPath, TimeSpan? healthPingInterval = null, TimeSpan? healthPongWindow = null) : IWorkerConnectionState
{
    private static readonly TimeSpan DefaultHealthPingInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DefaultHealthPongWindow = TimeSpan.FromSeconds(10);

    private readonly TimeSpan _healthPingInterval = healthPingInterval ?? DefaultHealthPingInterval;
    private readonly IpcReconnector _reconnector = new(new IpcReconnectPolicy());
    private readonly HealthLivenessMonitor _healthMonitor = new(healthPongWindow ?? DefaultHealthPongWindow);
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource> _pendingTerminal = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private volatile bool _isConnected;
    private NdjsonFrameWriter? _writer;

    /// <summary>Set once before <see cref="RunAsync"/> starts. Assigned after construction to break the constructor cycle with <see cref="SlackGateway"/>, which itself depends on this connection.</summary>
    public IWorkerEventListener? EventListener { get; set; }

    public bool IsConnected => _isConnected;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Stream stream;
            try
            {
                stream = await _reconnector.ConnectWithRetryAsync(ConnectAsync, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await using (stream)
            {
                await using var reader = new NdjsonFrameReader(stream);
                await using var writer = new NdjsonFrameWriter(stream);
                _writer = writer;
                _isConnected = true;
                Console.WriteLine("Broker connected to worker.");

                using var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var receiveTask = ReceiveLoopAsync(reader, connectionCts.Token);
                var healthTask = HealthLoopAsync(connectionCts.Token);

                await Task.WhenAny(receiveTask, healthTask).ConfigureAwait(false);

                _isConnected = false;
                _writer = null;
                Console.WriteLine("Broker disconnected from worker; reconnecting.");
                connectionCts.Cancel();

                try
                {
                    await receiveTask.ConfigureAwait(false);
                }
                catch
                {
                    // Connection teardown; RunAsync loops back to reconnect.
                }

                try
                {
                    await healthTask.ConfigureAwait(false);
                }
                catch
                {
                    // Connection teardown; RunAsync loops back to reconnect.
                }
            }
        }
    }

    /// <summary>Used as the dispatch delegate for <see cref="ExecutionScheduler.RunAsync"/>: submits a request and returns only once its terminal outcome has been forwarded.</summary>
    public async Task SubmitAsync(ExecutionRequestPayload request, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingTerminal[request.RequestId] = tcs;

        try
        {
            var envelope = EnvelopeCodec.Create(request.RequestId, request.CorrelationId, DateTimeOffset.UtcNow, request);
            await WriteAsync(envelope, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Not connected (or the write otherwise failed): give up on this submit so the
            // scheduler can move on rather than hang forever waiting for a terminal outcome
            // that will never arrive over a dead connection.
            _pendingTerminal.TryRemove(request.RequestId, out _);
            return;
        }

        using var registration = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        await tcs.Task.ConfigureAwait(false);
    }

    /// <summary>Sends a <c>CancelExecution</c> message for an in-flight request. A no-op signal to the worker if the request is unknown or already terminal there — the worker's own <c>IExecutionDispatcher.TryCancel</c> handles that safely.</summary>
    public Task RequestCancellationAsync(Guid requestId, string? reason, CancellationToken cancellationToken)
    {
        var envelope = EnvelopeCodec.Create(requestId, requestId, DateTimeOffset.UtcNow, new CancelExecutionPayload { RequestId = requestId, Reason = reason });
        return WriteAsync(envelope, cancellationToken);
    }

    private async Task<Stream> ConnectAsync(CancellationToken cancellationToken)
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), cancellationToken).ConfigureAwait(false);
        return new NetworkStream(socket, ownsSocket: true);
    }

    private async Task ReceiveLoopAsync(NdjsonFrameReader reader, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            MessageEnvelope? envelope;
            try
            {
                envelope = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                return;
            }

            if (envelope is null)
            {
                return;
            }

            await HandleEnvelopeAsync(envelope, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task HandleEnvelopeAsync(MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        switch (envelope.MessageType)
        {
            case MessageType.HealthPong:
                _healthMonitor.OnPongReceived();
                Console.WriteLine("Received HealthPong from worker.");
                break;

            case MessageType.ExecutionAccepted:
                var accepted = envelope.GetExecutionAcceptedPayload();
                if (EventListener is { } acceptedListener)
                {
                    await acceptedListener.AcceptedAsync(accepted, cancellationToken).ConfigureAwait(false);
                }

                break;

            case MessageType.ExecutionProgress:
                var progress = envelope.GetExecutionProgressPayload();
                if (EventListener is { } progressListener)
                {
                    await progressListener.ProgressAsync(progress, cancellationToken).ConfigureAwait(false);
                }

                break;

            case MessageType.ExecutionCompleted:
                var completed = envelope.GetExecutionCompletedPayload();
                if (EventListener is { } completedListener)
                {
                    await completedListener.CompletedAsync(completed, cancellationToken).ConfigureAwait(false);
                }

                CompleteTerminal(completed.RequestId);
                break;

            case MessageType.ExecutionFailed:
                var failed = envelope.GetExecutionFailedPayload();
                if (EventListener is { } failedListener)
                {
                    await failedListener.FailedAsync(failed, cancellationToken).ConfigureAwait(false);
                }

                CompleteTerminal(failed.RequestId);
                break;

            case MessageType.ExecutionCancelled:
                var cancelled = envelope.GetExecutionCancelledPayload();
                if (EventListener is { } cancelledListener)
                {
                    await cancelledListener.CancelledAsync(cancelled, cancellationToken).ConfigureAwait(false);
                }

                CompleteTerminal(cancelled.RequestId);
                break;
        }
    }

    private void CompleteTerminal(Guid requestId)
    {
        if (_pendingTerminal.TryRemove(requestId, out var tcs))
        {
            tcs.TrySetResult();
        }
    }

    private async Task HealthLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(_healthPingInterval, cancellationToken).ConfigureAwait(false);

                _healthMonitor.OnPingSent(DateTimeOffset.UtcNow);
                await WriteAsync(EnvelopeCodec.Create(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, new HealthPingPayload()), cancellationToken).ConfigureAwait(false);

                if (_healthMonitor.IsSessionBroken(DateTimeOffset.UtcNow))
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task WriteAsync(MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var writer = _writer ?? throw new InvalidOperationException("Not connected to the worker.");
            await writer.WriteAsync(envelope, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
