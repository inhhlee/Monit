using System;
using System.Collections.Generic;
using System.Text;

namespace Amr.Communication.Monitoring;

// 연결 세션마다 하나를 만듭니다. 정상 Heartbeat가 처음 도착한 AMR부터 감시합니다.
internal sealed class HeartbeatMonitor : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, AmrConnectionState> _amrs = new(StringComparer.Ordinal);
    private readonly HeartbeatMonitorOptions _options;
    private readonly IHeartbeatEventSink _sink;
    private readonly TimeProvider _time;
    private readonly ITimer _timer;
    private bool _stopped;

    internal HeartbeatMonitor(HeartbeatMonitorOptions options, IHeartbeatEventSink sink, TimeProvider? time = null)
    {
        options.Validate();
        _options = options;
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _time = time ?? TimeProvider.System;
        _timer = _time.CreateTimer(_ => CheckTimeouts(), null, options.CheckInterval, options.CheckInterval);
    }

    // Payload의 ts 대신 관제의 시계를 읽습니다. 검사·Core 대기 이전의 도착 시각입니다.
    internal HeartbeatReceipt CaptureReceipt() => new(_time.GetTimestamp(), _time.GetUtcNow().ToUnixTimeMilliseconds());

    internal void Observe(string amrId, HeartbeatReceipt receipt)
    {
        lock (_gate)
        {
            if (_stopped) return;
            if (!_amrs.TryGetValue(amrId, out var state))
            {
                state = new AmrConnectionState(receipt);
                _amrs.Add(amrId, state);
                Notify(amrId, HeartbeatNotificationKind.MonitoringStarted, state, receipt, TimeSpan.Zero);
                return;
            }
            // 동시 수신의 검사 완료 순서가 달라도 오래된 수신 시각으로 되돌리지 않습니다.
            if (receipt.Timestamp < state.LastReceived.Timestamp) return;
            var recovered = state.TimedOut;
            state.LastReceived = receipt;
            state.TimedOut = false;
            if (recovered) Notify(amrId, HeartbeatNotificationKind.Recovered, state, receipt, TimeSpan.Zero);
        }
    }

    private void CheckTimeouts()
    {
        lock (_gate)
        {
            if (_stopped) return;
            var now = CaptureReceipt();
            foreach (var (amrId, state) in _amrs)
            {
                // 단조 시계로 재므로 PC 날짜 변경이나 과거/미래 Payload ts가 판정을 바꾸지 않습니다.
                var elapsed = _time.GetElapsedTime(state.LastReceived.Timestamp, now.Timestamp);
                if (state.TimedOut || elapsed < _options.Timeout) continue;
                state.TimedOut = true;
                Notify(amrId, HeartbeatNotificationKind.TimedOut, state, now, elapsed);
            }
        }
    }

    private void Notify(string id, HeartbeatNotificationKind kind, AmrConnectionState state,
        HeartbeatReceipt now, TimeSpan elapsed)
    {
        try
        {
            _sink.OnHeartbeatChanged(new(id, kind, state.LastReceived.UnixMilliseconds,
                now.UnixMilliseconds, elapsed.TotalMilliseconds, _options.Timeout.TotalMilliseconds));
        }
        catch (Exception error)
        {
            // 알림 수신자 오류 때문에 MQTT 수신이나 다음 Timeout 검사가 멈추지 않게 합니다.
            try { System.Diagnostics.Trace.TraceError("Heartbeat observer failed: {0}", error.GetType().Name); }
            catch { }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_stopped) return;
            _stopped = true;
            _timer.Dispose();
            _amrs.Clear();
        }
    }
}

internal readonly record struct HeartbeatReceipt(long Timestamp, long UnixMilliseconds);
