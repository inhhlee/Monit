namespace Amr.Communication.Monitoring;

// 개발용 기본값입니다. Broker 연결 Timeout과는 별개의 AMR 생존 신호 기준입니다.
public sealed record HeartbeatMonitorOptions
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan CheckInterval { get; init; } = TimeSpan.FromMilliseconds(500);

    internal void Validate()
    {
        if (Timeout <= TimeSpan.Zero || Timeout > TimeSpan.FromDays(1))
            throw new ArgumentOutOfRangeException(nameof(Timeout), "Heartbeat timeout must be positive and at most one day.");
        // System 타이머의 밀리초 해상도보다 작은 주기가 0으로 반올림되어 반복 검사가 꺼지는 것을 막습니다.
        if (CheckInterval < TimeSpan.FromMilliseconds(1) || CheckInterval > Timeout)
            throw new ArgumentOutOfRangeException(nameof(CheckInterval), "Check interval must be at least 1ms and no greater than timeout.");
    }
}
