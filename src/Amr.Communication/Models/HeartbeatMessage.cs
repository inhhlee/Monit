namespace Amr.Communication.Models;

public sealed record HeartbeatMessage(string AmrId, long TimestampUnixMilliseconds)
    : IncomingMessage(AmrId, TimestampUnixMilliseconds)
{
    public override IncomingMessageKind Kind => IncomingMessageKind.Heartbeat;
}
