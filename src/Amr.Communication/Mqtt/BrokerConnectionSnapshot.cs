namespace Amr.Communication.Mqtt;

public enum BrokerConnectionState { Disconnected, Connecting, Connected, Disconnecting, Faulted, Disposed } //  Enum화 Broker 연결관리

/// <summary>Connection log/event, not business History. Timestamp unit: Unix milliseconds (Int64).</summary>
/// 
//  현재 상태, 왜 이유, 변경 시간, 오류 메시지 => Snapshot
public sealed record BrokerConnectionSnapshot(
    BrokerConnectionState State,
    string Reason,
    long TimestampUnixMilliseconds,
    string? Error = null);
