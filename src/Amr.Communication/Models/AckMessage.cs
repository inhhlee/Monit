namespace Amr.Communication.Models;

public enum AckResult { Success, Failure }

public sealed record AckMessage(string AmrId, long TimestampUnixMilliseconds, string CommandId,
    AckResult Result) : IncomingMessage(AmrId, TimestampUnixMilliseconds)
{
    public override IncomingMessageKind Kind => IncomingMessageKind.Ack;
}
