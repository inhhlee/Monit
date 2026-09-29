namespace Amr.Communication.Monitoring;

// 관측한 통신 현상입니다. 장비의 최종 Offline/운행 상태를 결정하는 이벤트는 아닙니다.
public enum HeartbeatNotificationKind { MonitoringStarted, TimedOut, Recovered }

public sealed record HeartbeatNotification(
    string AmrId,
    HeartbeatNotificationKind Kind,
    long LastReceivedUnixMilliseconds,
    long ObservedUnixMilliseconds,
    double ElapsedMilliseconds,
    double TimeoutMilliseconds);

// Core 또는 콘솔이 구현할 별도 알림 경계입니다. 알림 순서를 위해 직렬 호출합니다.
// 구현은 빨리 반환해야 하며, 이 콜백 안에서 연결 종료 등 수명주기 작업을 기다리지 않습니다.
public interface IHeartbeatEventSink
{
    void OnHeartbeatChanged(HeartbeatNotification notification);
}
