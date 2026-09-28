namespace Amr.Communication.Models;

public enum AmrState { Idle, Busy, Stopped, Fault }

public sealed record TelemetryPosition(double X, double Y);

public sealed record TelemetryMessage(string AmrId, long TimestampUnixMilliseconds, TelemetryPosition Position,
    double Speed, AmrState State)
    : IncomingMessage(AmrId, TimestampUnixMilliseconds)
{
    public override IncomingMessageKind Kind => IncomingMessageKind.Telemetry;
}
