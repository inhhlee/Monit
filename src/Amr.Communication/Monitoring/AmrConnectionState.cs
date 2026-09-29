using System;

namespace Amr.Communication.Monitoring;

// Heartbeat 관측 상태만 보관합니다. 실제 장비의 업무 상태나 최종 Offline 상태는 아닙니다.
internal sealed class AmrConnectionState(HeartbeatReceipt received)
{
    public HeartbeatReceipt LastReceived { get; set; } = received;
    public bool TimedOut { get; set; }
}
