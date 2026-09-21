namespace Amr.Communication.Models;

public enum IncomingMessageKind { Telemetry, Alarm, Heartbeat, Ack }

/// <summary>Validated internal contract v1. Timestamp is preserved as Unix milliseconds.</summary>
public abstract record IncomingMessage(string AmrId, long TimestampUnixMilliseconds)
{
    public abstract IncomingMessageKind Kind { get; }
}
