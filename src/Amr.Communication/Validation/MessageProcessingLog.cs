namespace Amr.Communication.Validation;

// 유효성 오류와 별개인 큐 초과·Core 처리 오류·종료 기록입니다.
public sealed record MessageProcessingFailure(string? Topic, string Code, string Reason);

public interface IMessageProcessingLog
{
    void Failed(MessageProcessingFailure failure);
}
