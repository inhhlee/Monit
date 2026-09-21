using Amr.Communication.Models;

namespace Amr.Communication.Validation;

/// <summary>Core-facing boundary. No Core business policy or database access belongs here.</summary>
public interface IIncomingMessageSink
{
    ValueTask ReceiveAsync(IncomingMessage message, CancellationToken cancellationToken);
}

public sealed record ValidationFailureLog(string? Topic, ValidationError Error);

public interface IValidationLog
{
    void Rejected(ValidationFailureLog failure);
}

public sealed class IncomingMessageProcessor
{
    private readonly MessageValidator _validator = new();
    private readonly IIncomingMessageSink _core;
    private readonly IValidationLog _log;

    public IncomingMessageProcessor(IIncomingMessageSink core, IValidationLog log)
        => (_core, _log) = (core ?? throw new ArgumentNullException(nameof(core)),
            log ?? throw new ArgumentNullException(nameof(log)));

    public async ValueTask<ValidationResult> ProcessAsync(string? topic, ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = _validator.Validate(topic, payload);
        if (result.IsValid)
            await _core.ReceiveAsync(result.Message!, cancellationToken).ConfigureAwait(false);
        else
            _log.Rejected(new(topic, result.Error!));
        return result;
    }
}
