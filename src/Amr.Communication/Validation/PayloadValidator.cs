using System.Text;
using System.Text.Json;
using Amr.Communication.Models;

namespace Amr.Communication.Validation;

internal static class PayloadValidator
{
    private const int MaxPayloadBytes = 65_536;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static ValidationResult Validate(string amrId, IncomingMessageKind kind, ReadOnlyMemory<byte> payload)
    {
        if (payload.IsEmpty)
            return ValidationResult.Rejected("EmptyPayload", "$", "Payload is empty.");
        if (payload.Length > MaxPayloadBytes)
            return ValidationResult.Rejected("PayloadTooLarge", "$", "Payload exceeds 65536 bytes.");
        try
        {
            // Invalid UTF-8 must never be replaced when materializing string fields.
            StrictUtf8.GetCharCount(payload.Span);
            using var document = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 16 });
            var root = document.RootElement;
            Object(root, "$", kind switch
            {
                IncomingMessageKind.Telemetry => ["id", "ts", "Pos"],
                IncomingMessageKind.Alarm => ["id", "ts", "code", "level", "message"],
                IncomingMessageKind.Heartbeat => ["id", "ts"],
                IncomingMessageKind.Ack => ["id", "ts", "cmdId", "result"],
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            });

            if (kind != IncomingMessageKind.Ack || root.TryGetProperty("id", out _))
            {
                var id = Text(root, "id");
                if (!string.Equals(amrId, id, StringComparison.Ordinal))
                    throw Invalid("IdMismatch", "$.id", "Payload ID differs from Topic ID.");
            }
            var timestamp = Required(root, "ts");
            if (timestamp.ValueKind != JsonValueKind.Number || !timestamp.TryGetInt64(out var ts))
                throw Invalid("InvalidType", "$.ts", "Expected a signed Int64 JSON integer (Unix milliseconds).");

            IncomingMessage message = kind switch
            {
                IncomingMessageKind.Telemetry => new TelemetryMessage(amrId, ts, Position(Required(root, "Pos"))),
                IncomingMessageKind.Alarm => new AlarmMessage(amrId, ts, Text(root, "code"), Level(Text(root, "level")), Text(root, "message")),
                IncomingMessageKind.Heartbeat => new HeartbeatMessage(amrId, ts),
                IncomingMessageKind.Ack => new AckMessage(amrId, ts, Text(root, "cmdId"), Result(Text(root, "result"))),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
            return ValidationResult.Accepted(message);
        }
        catch (PayloadException error)
        {
            return ValidationResult.Rejected(error.Code, error.Field, error.Message);
        }
        catch (DecoderFallbackException)
        {
            return ValidationResult.Rejected("InvalidUtf8", "$", "Payload is not valid UTF-8.");
        }
        catch (JsonException)
        {
            return ValidationResult.Rejected("InvalidJson", "$", "Malformed JSON or maximum depth exceeded.");
        }
    }

    private static void Object(JsonElement value, string path, string[] allowed)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw Invalid("InvalidType", path, "Expected a JSON object.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            // Do not copy arbitrary untrusted field names into diagnostic text.
            var name = DecodeString(() => property.Name, path)!;
            if (!seen.Add(name)) throw Invalid("DuplicateField", path, "Duplicate field in object.");
            if (!allowed.Contains(name, StringComparer.Ordinal))
                throw Invalid("UnexpectedField", path, "Field is not allowed for this message type.");
        }
    }

    private static JsonElement Required(JsonElement value, string name, string parent = "$") =>
        value.TryGetProperty(name, out var field) ? field :
        throw Invalid("MissingField", $"{parent}.{name}", "Required field is missing.");

    private static string Text(JsonElement value, string name, string parent = "$")
    {
        var field = Required(value, name, parent);
        if (field.ValueKind != JsonValueKind.String)
            throw Invalid("InvalidType", $"{parent}.{name}", "Expected a nonempty string.");
        var text = DecodeString(field.GetString, $"{parent}.{name}");
        if (string.IsNullOrWhiteSpace(text))
            throw Invalid("InvalidValue", $"{parent}.{name}", "String must not be empty or whitespace.");
        return text;
    }

    private static string? DecodeString(Func<string?> read, string path)
    {
        try { return read(); }
        // System.Text.Json defers unpaired UTF-16 escape validation until string access.
        // Limit this catch to string access so unrelated implementation errors still propagate.
        catch (InvalidOperationException)
        {
            throw Invalid("InvalidJson", path, "Invalid Unicode escape sequence in JSON string.");
        }
    }

    private static double Number(JsonElement value, string name)
    {
        var field = Required(value, name, "$.Pos");
        if (field.ValueKind != JsonValueKind.Number || !field.TryGetDouble(out var result))
            throw Invalid("InvalidType", $"$.Pos.{name}", "Expected a JSON number.");
        if (!double.IsFinite(result) || (name == "sp" && result < 0))
            throw Invalid("OutOfRange", $"$.Pos.{name}", "Expected a finite value; speed must be nonnegative.");
        return result;
    }

    private static TelemetryPosition Position(JsonElement pos)
    {
        Object(pos, "$.Pos", ["x", "y", "sp", "stat"]);
        return new(Number(pos, "x"), Number(pos, "y"), Number(pos, "sp"), State(Text(pos, "stat", "$.Pos")));
    }

    private static AmrState State(string state) => state switch
    {
        "IDLE" => AmrState.Idle, "MOVING" => AmrState.Moving,
        "CHARGING" => AmrState.Charging, "ERROR" => AmrState.Error,
        _ => throw Invalid("OutOfRange", "$.Pos.stat", "Unsupported AMR state.")
    };

    private static AlarmLevel Level(string level) => level switch
    {
        "INFO" => AlarmLevel.Info, "WARNING" => AlarmLevel.Warning, "ERROR" => AlarmLevel.Error,
        _ => throw Invalid("OutOfRange", "$.level", "Unsupported alarm level.")
    };

    private static AckResult Result(string result) => result switch
    {
        "SUCCESS" => AckResult.Success, "FAILURE" => AckResult.Failure,
        _ => throw Invalid("OutOfRange", "$.result", "Unsupported ACK result.")
    };

    private static PayloadException Invalid(string code, string field, string reason) => new(code, field, reason);

    private sealed class PayloadException(string code, string field, string reason) : Exception(reason)
    {
        public string Code { get; } = code;
        public string Field { get; } = field;
    }
}
