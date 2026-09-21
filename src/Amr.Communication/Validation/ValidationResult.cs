using Amr.Communication.Models;

namespace Amr.Communication.Validation;

public sealed record ValidationError(string Code, string Field, string Reason);

public sealed class ValidationResult
{
    private ValidationResult(IncomingMessage? message, ValidationError? error)
        => (Message, Error) = (message, error);

    public IncomingMessage? Message { get; }
    public ValidationError? Error { get; }
    public bool IsValid => Message is not null;

    internal static ValidationResult Accepted(IncomingMessage message) => new(message, null);
    internal static ValidationResult Rejected(string code, string field, string reason)
        => new(null, new(code, field, reason));
}
