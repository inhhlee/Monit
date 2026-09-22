using Amr.Communication.Models;

namespace Amr.Communication.Validation;

/// <summary>
/// 비즈니스 로직(Core)영역으로 향하는 경계  인터페이스이다.
/// 데이터베이스 접근이나 구체적인 비즈니스 정책은 구현체에서 처리하도록 한다.
/// </summary>
public interface IIncomingMessageSink
{
    // 검증이 완료된 메시지를 비즈니스 로직(Core)영역으로 전달한다.
    ValueTask ReceiveAsync(IncomingMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// 단일 메시지에 대한 검증 실패를 기록하기 위한 구조체이다.
/// </summary>
public sealed record ValidationFailureLog(string? Topic, ValidationError Error);

/// <summary>
/// 검증 실패를 기록하기 위한 인터페이스이다.
/// </summary>
public interface IValidationLog
{

    void Rejected(ValidationFailureLog failure);
}

/// <summary>
/// 외부에서 수신된 Raw 메시지의  유효성을 검사하고,
/// 성공하면 비즈니스 로직(Core)영역으로 전달하고, 실패하면 검증 실패를 기록한다.
/// </summary>
public sealed class IncomingMessageProcessor
{
    // 메시지 검증 담당
    private readonly MessageValidator _validator = new();

    // 정상메시지를 전달할 목적지 
    private readonly IIncomingMessageSink _core;

    // 실패메시지 로그
    private readonly IValidationLog _log;

    /// <summary>
    /// 정상메시지, 실패메시지 생성자 초기화
    /// </summary>    
    public IncomingMessageProcessor(IIncomingMessageSink core, IValidationLog log)
        => (_core, _log) = (core ?? throw new ArgumentNullException(nameof(core)),
            log ?? throw new ArgumentNullException(nameof(log)));

    /// <summary>
    /// 수신받은 메시지의 토픽과 페이로드를 검증합니다.
    /// </summary>
    public async ValueTask<ValidationResult> ProcessAsync(string? topic, ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        // 작업 취소 요청이 있는지 확인하고, 있으면 예외를 발생시킵니다.
        cancellationToken.ThrowIfCancellationRequested();

        // 토픽과 페이로드를 검증하고, 결과를 반환합니다.
        var result = _validator.Validate(topic, payload);
        if (result.IsValid)
            await _core.ReceiveAsync(result.Message!, cancellationToken).ConfigureAwait(false);
        else
            _log.Rejected(new(topic, result.Error!));
        return result;
    }
}
