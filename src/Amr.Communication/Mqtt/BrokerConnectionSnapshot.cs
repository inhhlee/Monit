namespace Amr.Communication.Mqtt;

public enum BrokerConnectionState { Disconnected, Connecting, Connected, Disconnecting, Faulted, Disposed }

/// <summary>Connection log/event, not business History. Timestamp unit: Unix milliseconds (Int64).</summary>
public sealed record BrokerConnectionSnapshot(
    BrokerConnectionState State,
    string Reason,
    long TimestampUnixMilliseconds,
    string? Error = null);
