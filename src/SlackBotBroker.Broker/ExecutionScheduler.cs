using System.Collections.Concurrent;
using System.Threading.Channels;
using SlackBotBroker.Protocol.Payloads;

namespace SlackBotBroker.Broker;

/// <summary>
/// Buffers admitted execution requests in a bounded FIFO queue and, by default, dispatches only
/// one at a time — the next admitted request is not read from the queue until the caller-supplied
/// dispatch delegate for the current one completes (i.e. reaches a terminal outcome).
/// </summary>
public sealed class ExecutionScheduler
{
    private readonly Channel<ExecutionRequestPayload> _channel;
    private readonly ConcurrentDictionary<Guid, byte> _active = new();

    public ExecutionScheduler(int capacity)
    {
        _channel = Channel.CreateBounded<ExecutionRequestPayload>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });
    }

    /// <summary>Admits a request if the queue has capacity. Returns false immediately, without enqueuing, when the queue is full.</summary>
    public bool TryAdmit(ExecutionRequestPayload request) => _channel.Writer.TryWrite(request);

    /// <summary>True while <paramref name="requestId"/> is queued or being actively dispatched; false once its terminal outcome has been delivered.</summary>
    public bool IsActive(Guid requestId) => _active.ContainsKey(requestId);

    /// <summary>
    /// Reads admitted requests in FIFO order and awaits <paramref name="dispatch"/> for each
    /// before reading the next — the single-global-execution default. The caller's
    /// <paramref name="dispatch"/> delegate is expected to return only once the request has
    /// reached a terminal outcome.
    /// </summary>
    public async Task RunAsync(Func<ExecutionRequestPayload, CancellationToken, Task> dispatch, CancellationToken cancellationToken)
    {
        await foreach (var request in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            _active[request.RequestId] = 0;
            try
            {
                await dispatch(request, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _active.TryRemove(request.RequestId, out _);
            }
        }
    }
}
