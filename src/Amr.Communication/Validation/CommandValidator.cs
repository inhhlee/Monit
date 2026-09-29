using Amr.Communication.Models;

namespace Amr.Communication.Validation;

public sealed record CommandValidationResult(CommandMessage? Message, string? CommandId, ValidationError? Error)
{
    public bool IsValid => Message is not null;
}

// 관제 발행 전과 가상 AMR 수신 시 같은 형식 검사를 사용합니다.
public sealed class CommandValidator
{
    public CommandValidationResult Validate(ReadOnlyMemory<byte> payload)
        => PayloadValidator.ValidateCommand(payload);
}
