using System.Collections.Concurrent;
using System.Text;
using Amr.Communication.Models;
using Amr.Communication.Mqtt;
using Amr.Communication.Validation;

namespace Amr.Communication.Tests;

public class AmrMessageDispatcherTests
{
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static Task Ready(Task task) => task.WaitAsync(TimeSpan.FromSeconds(5));
    private static byte[] Heartbeat(string id, long ts) => Encoding.UTF8.GetBytes($$"""{"id":"{{id}}","ts":{{ts}},"status":"OK"}""");
    private static bool Send(AmrMessageDispatcher dispatcher, string id, long ts)
        => dispatcher.TryEnqueue($"amr/{id}/heartbeat", Heartbeat(id, ts));

    [Fact]
    public async Task SameAmrWaitsInOrderWhileOtherAmrCompletes()
    {
        var entered = Signal(); var release = Signal(); var bDone = Signal(); var aDone = Signal();
        var probe = new Probe(async (message, token) =>
        {
            if (message.AmrId == "A" && message.TimestampUnixMilliseconds == 1)
            { entered.TrySetResult(); await release.Task.WaitAsync(token); }
            if (message.AmrId == "B") bDone.TrySetResult();
            if (message.AmrId == "A" && message.TimestampUnixMilliseconds == 3) aDone.TrySetResult();
        });
        await using var dispatcher = probe.Dispatcher();
        Assert.True(Send(dispatcher, "A", 1));
        await Ready(entered.Task);
        Assert.True(Send(dispatcher, "A", 2));
        Assert.True(Send(dispatcher, "A", 3));
        Assert.True(Send(dispatcher, "B", 4));
        await Ready(bDone.Task);
        Assert.Equal(new long[] { 1 }, probe.Started.Where(x => x.AmrId == "A").Select(x => x.TimestampUnixMilliseconds));
        release.TrySetResult();
        await Ready(aDone.Task);
        Assert.Equal(new long[] { 1, 2, 3 }, probe.Started.Where(x => x.AmrId == "A").Select(x => x.TimestampUnixMilliseconds));
    }

    [Fact]
    public async Task DifferentMessageTypesOfSameAmrShareOneQueue()
    {
        var entered = Signal(); var release = Signal(); var ackDone = Signal(); var bDone = Signal();
        var probe = new Probe(async (message, token) =>
        {
            if (message.AmrId == "A" && message.Kind == IncomingMessageKind.Heartbeat)
            { entered.TrySetResult(); await release.Task.WaitAsync(token); }
            if (message.Kind == IncomingMessageKind.Ack) ackDone.TrySetResult();
            if (message.AmrId == "B") bDone.TrySetResult();
        });
        await using var dispatcher = probe.Dispatcher();
        Send(dispatcher, "A", 1); await Ready(entered.Task);
        Assert.True(dispatcher.TryEnqueue("amr/A/ack", Encoding.UTF8.GetBytes("""{"cmdId":"C1","ts":2,"result":"FAILURE","reason":"test"}""")));
        Send(dispatcher, "B", 3); await Ready(bDone.Task);
        Assert.DoesNotContain(probe.Started, x => x.Kind == IncomingMessageKind.Ack);
        release.TrySetResult(); await Ready(ackDone.Task);
        Assert.Equal(new[] { IncomingMessageKind.Heartbeat, IncomingMessageKind.Ack }, probe.Started.Where(x => x.AmrId == "A").Select(x => x.Kind));
    }

    [Fact]
    public async Task FullQueueRejectsOnlyNewMessageAndDoesNotBlockAnotherAmr()
    {
        var entered = Signal(); var release = Signal(); var bDone = Signal(); var aDone = Signal();
        var probe = new Probe(async (message, token) =>
        {
            if (message.AmrId == "A" && message.TimestampUnixMilliseconds == 1)
            { entered.TrySetResult(); await release.Task.WaitAsync(token); }
            if (message.AmrId == "B") bDone.TrySetResult();
            if (message.TimestampUnixMilliseconds == 2) aDone.TrySetResult();
        });
        await using var dispatcher = probe.Dispatcher(capacity: 1);
        Send(dispatcher, "A", 1); await Ready(entered.Task);
        Assert.True(Send(dispatcher, "A", 2));
        Assert.False(Send(dispatcher, "A", 3));
        Assert.True(Send(dispatcher, "B", 4)); await Ready(bDone.Task);
        Assert.Contains(probe.Failures, x => x.Code == "QueueFull" && x.Topic == "amr/A/heartbeat");
        Assert.Empty(probe.Invalid);
        release.TrySetResult(); await Ready(aDone.Task);
        Assert.DoesNotContain(probe.Started, x => x.TimestampUnixMilliseconds == 3);
    }

    [Fact]
    public async Task InvalidInputDoesNotConsumeAmrSlotAndExistingAmrWorksAtLimit()
    {
        var delivered = Signal();
        var probe = new Probe((_, _) => { delivered.TrySetResult(); return ValueTask.CompletedTask; });
        await using var dispatcher = probe.Dispatcher(maxAmrs: 1);
        Assert.False(dispatcher.TryEnqueue("amr/B/heartbeat", Encoding.UTF8.GetBytes("{")));
        Assert.True(Send(dispatcher, "A", 1)); await Ready(delivered.Task);
        Assert.False(Send(dispatcher, "B", 2));
        Assert.True(Send(dispatcher, "A", 3));
        Assert.Single(probe.Invalid);
        Assert.Contains(probe.Failures, x => x.Code == "AmrLimitReached");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandlerOrProcessingLoggerFailureDoesNotKillWorker(bool throwingLogger)
    {
        var done = Signal();
        var probe = new Probe((message, _) =>
        {
            if (message.TimestampUnixMilliseconds == 1) throw new IOException("do not log this payload-like text");
            done.TrySetResult(); return ValueTask.CompletedTask;
        }) { ThrowProcessingLog = throwingLogger };
        await using var dispatcher = probe.Dispatcher();
        Send(dispatcher, "A", 1); Send(dispatcher, "A", 2);
        await Ready(done.Task);
        Assert.Empty(probe.Invalid);
        var failure = Assert.Single(probe.Failures);
        Assert.Equal("HandlerFailed", failure.Code);
        Assert.Equal("IOException", failure.Reason);
    }

    [Fact]
    public async Task StopCancelsActiveWorkDiscardsPendingAndRejectsLateInput()
    {
        var entered = Signal(); var cancelled = Signal();
        var probe = new Probe(async (_, token) =>
        {
            entered.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, token); }
            catch (OperationCanceledException) { cancelled.TrySetResult(); throw; }
        });
        await using var dispatcher = probe.Dispatcher();
        Send(dispatcher, "A", 1); await Ready(entered.Task); Send(dispatcher, "A", 2);
        await Ready(dispatcher.StopAsync()); await Ready(cancelled.Task);
        Assert.False(Send(dispatcher, "A", 3));
        Assert.Single(probe.Started);
        Assert.Contains(probe.Failures, x => x.Code == "ProcessingCancelled");
        Assert.Contains(probe.Failures, x => x.Code == "ShutdownDiscarded");
        Assert.Contains(probe.Failures, x => x.Code == "ReceiverStopped");
        await dispatcher.StopAsync();
    }

    [Fact]
    public async Task StopWaitCanBeCancelledWithoutDeliveringQueuedWorkLater()
    {
        var entered = Signal(); var release = Signal();
        var probe = new Probe(async (_, _) => { entered.TrySetResult(); await release.Task; });
        await using var dispatcher = probe.Dispatcher();
        Send(dispatcher, "A", 1); await Ready(entered.Task); Send(dispatcher, "A", 2);
        using var cancellation = new CancellationTokenSource();
        var stop = dispatcher.StopAsync(cancellation.Token);
        cancellation.Cancel();
        try { await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stop); }
        finally { release.TrySetResult(); }
        await Ready(dispatcher.StopAsync());
        Assert.Single(probe.Started);
    }

    [Fact]
    public async Task ConcurrentFirstMessagesCreateOneWorkerPerAmr()
    {
        var all = Signal(); var count = 0; var active = 0; var overlap = 0;
        var probe = new Probe(async (_, _) =>
        {
            if (Interlocked.Increment(ref active) != 1) Interlocked.Exchange(ref overlap, 1);
            await Task.Yield();
            Interlocked.Decrement(ref active);
            if (Interlocked.Increment(ref count) == 100) all.TrySetResult();
        });
        await using var dispatcher = probe.Dispatcher();
        await Task.WhenAll(Enumerable.Range(1, 100).Select(i => Task.Run(() => Assert.True(Send(dispatcher, "A", i)))));
        await Ready(all.Task);
        Assert.Equal(0, overlap);
        Assert.Equal(100, probe.Started.Select(x => x.TimestampUnixMilliseconds).Distinct().Count());
    }

    private sealed class Probe(Func<IncomingMessage, CancellationToken, ValueTask> handle) : IIncomingMessageSink, IValidationLog, IMessageProcessingLog
    {
        public ConcurrentQueue<IncomingMessage> Started { get; } = new();
        public ConcurrentQueue<ValidationFailureLog> Invalid { get; } = new();
        public ConcurrentQueue<MessageProcessingFailure> Failures { get; } = new();
        public bool ThrowProcessingLog { get; init; }
        public AmrMessageDispatcher Dispatcher(int capacity = 256, int maxAmrs = 64)
            => new(new(this, this, this), capacity, maxAmrs);
        public ValueTask ReceiveAsync(IncomingMessage message, CancellationToken token)
        { Started.Enqueue(message); return handle(message, token); }
        public void Rejected(ValidationFailureLog failure) => Invalid.Enqueue(failure);
        public void Failed(MessageProcessingFailure failure)
        { Failures.Enqueue(failure); if (ThrowProcessingLog) throw new IOException("logger failed"); }
    }
}
