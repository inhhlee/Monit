namespace Amr.Communication.Models;

public enum AlarmLevel { Info, Warning, Error }

public sealed record AlarmMessage(string AmrId, long TimestampUnixMilliseconds, string Code,
    AlarmLevel Level, string Message) : IncomingMessage(AmrId, TimestampUnixMilliseconds)
{
    public override IncomingMessageKind Kind => IncomingMessageKind.Alarm;
}
