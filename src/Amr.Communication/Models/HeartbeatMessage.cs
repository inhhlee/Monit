namespace Amr.Communication.Models;

public enum HeartbeatStatus { Ok }

public sealed record HeartbeatMessage(string AmrId, long TimestampUnixMilliseconds, HeartbeatStatus Status)
    : IncomingMessage(AmrId, TimestampUnixMilliseconds)
{
    public override IncomingMessageKind Kind => IncomingMessageKind.Heartbeat;
}
