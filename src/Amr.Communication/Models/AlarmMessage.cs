namespace Amr.Communication.Models;

public enum AlarmLevel { Info, Warning, Error, Fatal }
public enum AlarmState { Active, Cleared }

public sealed record AlarmMessage(string AmrId, long TimestampUnixMilliseconds, string Code,
    AlarmLevel Level, AlarmState AlarmState, string? Message) : IncomingMessage(AmrId, TimestampUnixMilliseconds)
{
    public override IncomingMessageKind Kind => IncomingMessageKind.Alarm;
}
