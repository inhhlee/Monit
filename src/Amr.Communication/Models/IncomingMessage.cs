namespace Amr.Communication.Models;

public enum IncomingMessageKind { Telemetry, Alarm, Heartbeat, Ack }

/// <summary>
/// 수신된 메시지의 AMR ID와 타임스탬프를 나타내는 추상 레코드이다.
/// </summary>
public abstract record IncomingMessage(string AmrId, long TimestampUnixMilliseconds)
{
    // enum화된 메시지 종류 get
    public abstract IncomingMessageKind Kind { get; }
}
