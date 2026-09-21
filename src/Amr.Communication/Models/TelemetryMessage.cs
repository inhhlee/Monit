namespace Amr.Communication.Models;

public enum AmrState { Idle, Moving, Charging, Error }

public sealed record TelemetryPosition(double X, double Y, double Speed, AmrState State);

public sealed record TelemetryMessage(string AmrId, long TimestampUnixMilliseconds, TelemetryPosition Position)
    : IncomingMessage(AmrId, TimestampUnixMilliseconds)
{
    public override IncomingMessageKind Kind => IncomingMessageKind.Telemetry;
}
