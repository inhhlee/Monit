using System.Threading.Channels;
using Amr.Communication.Models;
using Amr.Communication.Validation;

namespace Amr.Communication.Mqtt;

// 연결 세션이 소유합니다. 같은 AMR은 순차 처리하고 다른 AMR은 별도로 진행합니다.
internal sealed class AmrMessageDispatcher : IDisposable, IAsyncDisposable
{
    private readonly IncomingMessageProcessor _processor;
    private readonly int _capacity;
    private readonly int _maxAmrs;
    private readonly object _gate = new();
    private readonly Dictionary<string, Worker> _workers = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _stop = new();
    private volatile bool _stopping;
    private Task? _shutdown;

    public AmrMessageDispatcher(IncomingMessageProcessor processor, int capacity = 256, int maxAmrs = 64)
    {
        ArgumentNullException.ThrowIfNull(processor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxAmrs);
        (_processor, _capacity, _maxAmrs) = (processor, capacity, maxAmrs);
    }

    public bool TryEnqueue(string topic, ReadOnlyMemory<byte> payload)
    {
        var result = _processor.Validate(topic, payload);
        if (!result.IsValid) return false;
        var message = result.Message!;
        string code;
        string reason;
        lock (_gate)
        {
            if (_shutdown is not null)
                (code, reason) = ("ReceiverStopped", "Connection session is stopping; message not queued.");
            else
            {
                if (!_workers.TryGetValue(message.AmrId, out var worker) && _workers.Count < _maxAmrs)
                {
                    worker = new Worker(_capacity);
                    _workers.Add(message.AmrId, worker);
                    worker.Task = Task.Run(() => ConsumeAsync(worker));
                }
                if (worker is null)
                    (code, reason) = ("AmrLimitReached", "AMR queue limit reached for this connection session.");
                else if (worker.Queue.Writer.TryWrite(new(topic, message))) return true;
                else
                    (code, reason) = ("QueueFull", "AMR queue is full; new message rejected without blocking other AMRs.");
            }
        }
        _processor.ProcessingFailed(topic, code, reason);
        return false;
    }

    private async Task ConsumeAsync(Worker worker)
    {
        var token = _stop.Token;
        try
        {
            await foreach (var item in worker.Queue.Reader.ReadAllAsync(token).ConfigureAwait(false))
            {
                if (_stopping || token.IsCancellationRequested)
                {
                    _processor.ProcessingFailed(item.Topic, "ShutdownDiscarded", "Queued message discarded during session shutdown.");
                    break;
                }
                try { await _processor.DeliverAsync(item.Message, token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    _processor.ProcessingFailed(item.Topic, "ProcessingCancelled", "Core processing cancelled during session shutdown.");
                    break;
                }
                catch (Exception error)
                {
                    _processor.ProcessingFailed(item.Topic, "HandlerFailed", error.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally
        {
            var discarded = 0;
            string? topic = null;
            while (worker.Queue.Reader.TryRead(out var item)) { discarded++; topic = item.Topic; }
            if (discarded > 0)
                _processor.ProcessingFailed(topic, "ShutdownDiscarded", $"Discarded {discarded} queued messages during session shutdown.");
        }
    }

    private Task RequestStop()
    {
        lock (_gate)
        {
            if (_shutdown is not null) return _shutdown;
            _stopping = true;
            var workers = _workers.Values.ToArray();
            foreach (var worker in workers) worker.Queue.Writer.TryComplete();
            // 동기 Dispose에서도 Core의 취소 콜백/작업 완료로 수신 스레드를 막지 않습니다.
            _shutdown = Task.Run(async () =>
            {
                try { await _stop.CancelAsync().ConfigureAwait(false); }
                catch (Exception error) { _processor.ProcessingFailed(null, "CancellationFailed", error.GetType().Name); }
                try { await Task.WhenAll(workers.Select(worker => worker.Task)).ConfigureAwait(false); }
                finally { _stop.Dispose(); }
            });
            return _shutdown;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
        => RequestStop().WaitAsync(cancellationToken);
    public void Dispose() => _ = RequestStop();
    public ValueTask DisposeAsync() => new(RequestStop());

    private sealed record QueuedMessage(string Topic, IncomingMessage Message);
    private sealed class Worker(int capacity)
    {
        public Channel<QueuedMessage> Queue { get; } = Channel.CreateBounded<QueuedMessage>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true, SingleWriter = false, AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait // TryWrite만 사용하므로 큐가 가득 차면 기다리지 않습니다.
        });
        public Task Task { get; set; } = Task.CompletedTask;
    }
}
