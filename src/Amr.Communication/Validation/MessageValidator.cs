using Amr.Communication.Models;

namespace Amr.Communication.Validation;

/// <summary>Topic and payload validation for the provisional COM-04 contract.</summary>
public sealed class MessageValidator
{
    public ValidationResult Validate(string? topic, ReadOnlyMemory<byte> payload)
    {
        var parts = topic?.Split('/');
        if (parts is not { Length: 3 } || parts[0] != "amr" || !IsValidId(parts[1]))
            return ValidationResult.Rejected("UnsupportedTopic", "$topic", "Expected amr/{amrId}/{kind} with a valid ID.");

        IncomingMessageKind? kind = parts[2] switch
        {
            "telemetry" => IncomingMessageKind.Telemetry,
            "alarm" => IncomingMessageKind.Alarm,
            "heartbeat" => IncomingMessageKind.Heartbeat,
            "ack" => IncomingMessageKind.Ack,
            _ => null
        };
        return kind is null
            ? ValidationResult.Rejected("UnsupportedTopic", "$topic", "Unsupported incoming message kind.")
            : PayloadValidator.Validate(parts[1], kind.Value, payload);
    }

    private static bool IsValidId(string id) => id.Length > 0 &&
        !id.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c is '/' or '+' or '#');
}
