using System.Collections.Concurrent;
using System.Text;
using Amr.Communication.Models;
using Amr.Communication.Monitoring;
using Amr.Communication.Mqtt;
using Amr.Communication.Validation;

namespace Amr.Communication.Tests;

public class HeartbeatMonitorTests
{
    private static readonly HeartbeatMonitorOptions Options = new()
    { Timeout = TimeSpan.FromSeconds(5), CheckInterval = TimeSpan.FromMilliseconds(500) };
    private static byte[] Heartbeat(string id, long ts = 1) =>
        Encoding.UTF8.GetBytes($$"""{"id":"{{id}}","ts":{{ts}},"status":"OK"}""");

    [Fact]
    public void FirstValidReceiptStartsMonitoringAndNoUnknownAmrTimeout()
    {
        var clock = new TestClock(); var sink = new EventSink();
        using var monitor = new HeartbeatMonitor(Options, sink, clock);
        clock.Advance(10000); clock.Tick(); Assert.Empty(sink.Events);
        monitor.Observe("A", monitor.CaptureReceipt());
        Assert.Equal(HeartbeatNotificationKind.MonitoringStarted, Assert.Single(sink.Events).Kind);
        clock.Advance(4999); clock.Tick(); Assert.Single(sink.Events);
    }

    [Theory]
    [InlineData(4999, false)]
    [InlineData(5000, true)]
    [InlineData(5001, true)]
    public void TimeoutBoundaryUsesElapsedReceiptTime(int milliseconds, bool expired)
    {
        var clock = new TestClock(); var sink = new EventSink();
        using var monitor = new HeartbeatMonitor(Options, sink, clock);
        var received = monitor.CaptureReceipt(); monitor.Observe("A", received);
        clock.Advance(milliseconds); clock.Tick();
        var timeouts = sink.Events.Where(x => x.Kind == HeartbeatNotificationKind.TimedOut).ToArray();
        Assert.Equal(expired ? 1 : 0, timeouts.Length);
        if (expired)
        {
            var timeout = Assert.Single(timeouts);
            Assert.Equal("A", timeout.AmrId);
            Assert.Equal(received.UnixMilliseconds, timeout.LastReceivedUnixMilliseconds);
            Assert.Equal(received.UnixMilliseconds + milliseconds, timeout.ObservedUnixMilliseconds);
            Assert.Equal(milliseconds, timeout.ElapsedMilliseconds);
            Assert.Equal(5000, timeout.TimeoutMilliseconds);
        }
    }

    [Fact]
    public void RefreshIsPerAmrAndRepeatedTicksEmitOnlyOneTimeout()
    {
        var clock = new TestClock(); var sink = new EventSink();
        using var monitor = new HeartbeatMonitor(Options, sink, clock);
        monitor.Observe("A", monitor.CaptureReceipt()); monitor.Observe("B", monitor.CaptureReceipt());
        for (var i = 0; i < 12; i++)
        {
            clock.Advance(500); monitor.Observe("B", monitor.CaptureReceipt()); clock.Tick();
        }
        Assert.Equal("A", Assert.Single(sink.Events, x => x.Kind == HeartbeatNotificationKind.TimedOut).AmrId);
        monitor.Observe("A", monitor.CaptureReceipt()); monitor.Observe("A", monitor.CaptureReceipt());
        Assert.Single(sink.Events, x => x.Kind == HeartbeatNotificationKind.Recovered);
        clock.Advance(5000); clock.Tick();
        Assert.Equal(2, sink.Events.Count(x => x.AmrId == "A" && x.Kind == HeartbeatNotificationKind.TimedOut));
    }

    [Theory]
    [InlineData(-86400000)]
    [InlineData(86400000)]
    public void WallClockChangesDoNotChangeElapsedTimeout(int shift)
    {
        var clock = new TestClock(); var sink = new EventSink();
        using var monitor = new HeartbeatMonitor(Options, sink, clock);
        monitor.Observe("A", monitor.CaptureReceipt());
        clock.ShiftUtc(shift); clock.Advance(4999); clock.Tick(); Assert.Single(sink.Events);
        clock.Advance(1); clock.Tick(); Assert.Equal(5000, sink.Events.Last().ElapsedMilliseconds);
    }

    [Fact]
    public void OutOfOrderObservationDoesNotReplaceNewerReceipt()
    {
        var clock = new TestClock(); var sink = new EventSink();
        using var monitor = new HeartbeatMonitor(Options, sink, clock);
        var old = monitor.CaptureReceipt(); clock.Advance(1000);
        var recent = monitor.CaptureReceipt(); monitor.Observe("A", recent); monitor.Observe("A", old);
        clock.Advance(4000); clock.Tick(); Assert.Single(sink.Events);
        clock.Advance(1000); clock.Tick(); Assert.Equal(recent.UnixMilliseconds, sink.Events.Last().LastReceivedUnixMilliseconds);
    }

    [Fact]
    public void DisposalSuppressesLateTimerCallbacksAndNewObservations()
    {
        var clock = new TestClock(); var sink = new EventSink();
        var monitor = new HeartbeatMonitor(Options, sink, clock);
        monitor.Observe("A", monitor.CaptureReceipt()); monitor.Dispose(); monitor.Dispose();
        clock.Advance(10000); clock.Tick(); monitor.Observe("B", monitor.CaptureReceipt());
        Assert.Single(sink.Events); Assert.True(clock.Timer!.Disposed);
    }

    [Fact]
    public void ObserverExceptionDoesNotStopOtherAmrOrRecovery()
    {
        var clock = new TestClock(); var sink = new EventSink { Throw = true };
        using var monitor = new HeartbeatMonitor(Options, sink, clock);
        monitor.Observe("A", monitor.CaptureReceipt()); monitor.Observe("B", monitor.CaptureReceipt());
        clock.Advance(5000); clock.Tick();
        Assert.Equal(2, sink.Events.Count(x => x.Kind == HeartbeatNotificationKind.TimedOut));
        monitor.Observe("A", monitor.CaptureReceipt());
        Assert.Equal(HeartbeatNotificationKind.Recovered, sink.Events.Last().Kind);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(-1, 100)]
    [InlineData(5000, 0)]
    [InlineData(5000, 0.5)]
    [InlineData(5000, 5001)]
    [InlineData(86400001, 100)]
    public void InvalidTimingSettingsAreRejected(double timeout, double interval)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HeartbeatMonitor(new()
        { Timeout = TimeSpan.FromMilliseconds(timeout), CheckInterval = TimeSpan.FromMilliseconds(interval) }, new EventSink(), new TestClock()));
    }

    [Fact]
    public async Task PayloadTimeAndInvalidHeartbeatCannotPostponeTimeout()
    {
        var clock = new TestClock(); var events = new EventSink(); var sink = new MessageSink();
        using var monitor = new HeartbeatMonitor(Options, events, clock);
        await using var dispatcher = new AmrMessageDispatcher(new(sink, sink, sink), heartbeats: monitor);
        Assert.True(dispatcher.TryEnqueue("amr/A/heartbeat", Heartbeat("A", 1)));
        clock.Advance(1000);
        Assert.True(dispatcher.TryEnqueue("amr/B/heartbeat", Heartbeat("B", long.MaxValue)));
        clock.Advance(3999);
        foreach (var payload in new[] { "{", "{\"id\":\"A\",\"ts\":9999999999999}", "{\"id\":\"B\",\"ts\":1,\"status\":\"OK\"}" })
            Assert.False(dispatcher.TryEnqueue("amr/A/heartbeat", Encoding.UTF8.GetBytes(payload)));
        clock.Tick(); Assert.DoesNotContain(events.Events, e => e.Kind == HeartbeatNotificationKind.TimedOut);
        clock.Advance(1); clock.Tick();
        Assert.Equal("A", Assert.Single(events.Events, e => e.Kind == HeartbeatNotificationKind.TimedOut).AmrId);
        clock.Advance(1000); clock.Tick(); Assert.Equal(2, events.Events.Count(e => e.Kind == HeartbeatNotificationKind.TimedOut));
        Assert.Equal(3, sink.Invalid.Count);
    }

    [Fact]
    public async Task FullQueueAndBlockedCoreDoNotDelayHeartbeatRefresh()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var clock = new TestClock(); var events = new EventSink();
        var sink = new MessageSink(async (_, token) => { entered.TrySetResult(); await Task.Delay(Timeout.Infinite, token); });
        using var monitor = new HeartbeatMonitor(Options, events, clock);
        await using var dispatcher = new AmrMessageDispatcher(new(sink, sink, sink), capacity: 1, heartbeats: monitor);
        Assert.True(dispatcher.TryEnqueue("amr/A/heartbeat", Heartbeat("A")));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(dispatcher.TryEnqueue("amr/A/heartbeat", Heartbeat("A", 2)));
        clock.Advance(4999);
        Assert.False(dispatcher.TryEnqueue("amr/A/heartbeat", Heartbeat("A", 3)));
        clock.Advance(1); clock.Tick();
        Assert.DoesNotContain(events.Events, e => e.Kind == HeartbeatNotificationKind.TimedOut);
        Assert.Contains(sink.Failures, e => e.Code == "QueueFull");
        clock.Advance(4999); clock.Tick();
        Assert.Equal(4999 + TestClock.Epoch, events.Events.Last().LastReceivedUnixMilliseconds);
        await dispatcher.StopAsync();
        var count = events.Events.Count; clock.Advance(10000); clock.Tick();
        Assert.False(dispatcher.TryEnqueue("amr/B/heartbeat", Heartbeat("B")));
        Assert.Equal(count, events.Events.Count);
    }

    [Fact]
    public async Task OtherMessageTypesAndRejectedAmrDoNotStartHeartbeatMonitoring()
    {
        var clock = new TestClock(); var events = new EventSink(); var sink = new MessageSink();
        using var monitor = new HeartbeatMonitor(Options, events, clock);
        await using var dispatcher = new AmrMessageDispatcher(new(sink, sink, sink), maxAmrs: 1, heartbeats: monitor);
        Assert.True(dispatcher.TryEnqueue("amr/A/ack", Encoding.UTF8.GetBytes("""{"cmdId":"C1","ts":1,"result":"FAILURE"}""")));
        Assert.False(dispatcher.TryEnqueue("amr/B/heartbeat", Heartbeat("B")));
        clock.Advance(10000); clock.Tick(); Assert.Empty(events.Events);
    }

    private sealed class EventSink : IHeartbeatEventSink
    {
        public List<HeartbeatNotification> Events { get; } = new();
        public bool Throw { get; init; }
        public void OnHeartbeatChanged(HeartbeatNotification notification)
        { Events.Add(notification); if (Throw) throw new IOException("observer failure"); }
    }

    private sealed class MessageSink(Func<IncomingMessage, CancellationToken, ValueTask>? handle = null)
        : IIncomingMessageSink, IValidationLog, IMessageProcessingLog
    {
        public ConcurrentQueue<ValidationFailureLog> Invalid { get; } = new();
        public ConcurrentQueue<MessageProcessingFailure> Failures { get; } = new();
        public ValueTask ReceiveAsync(IncomingMessage message, CancellationToken cancellationToken)
            => handle?.Invoke(message, cancellationToken) ?? ValueTask.CompletedTask;
        public void Rejected(ValidationFailureLog failure) => Invalid.Enqueue(failure);
        public void Failed(MessageProcessingFailure failure) => Failures.Enqueue(failure);
    }

    // 시간을 직접 제어하는 테스트 시계입니다. Tick은 종료 후 늦게 도착하는 타이머 호출도 재현합니다.
    private sealed class TestClock : TimeProvider
    {
        public const long Epoch = 1790000000000;
        private long _milliseconds;
        private long _utcOffset;
        public TestTimer? Timer { get; private set; }
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => _milliseconds;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeMilliseconds(Epoch + _milliseconds + _utcOffset);
        public void Advance(long milliseconds) => _milliseconds += milliseconds;
        public void ShiftUtc(long milliseconds) => _utcOffset += milliseconds;
        public void Tick() => Timer!.Fire();
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Assert.Equal(Options.CheckInterval, dueTime); Assert.Equal(Options.CheckInterval, period);
            return Timer = new(callback, state);
        }
    }
    private sealed class TestTimer(TimerCallback callback, object? state) : ITimer
    {
        public bool Disposed { get; private set; }
        public void Fire() => callback(state);
        public bool Change(TimeSpan dueTime, TimeSpan period) => !Disposed;
        public void Dispose() => Disposed = true;
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
