using System.Text.Json;
using Amr.Communication.Validation;

namespace Amr.TestClient;

public sealed record CommandResponse(string? AckPayload, ValidationError? Error);

public static class BasicCommandHandler
{
    public static CommandResponse Handle(string amrId, ReadOnlyMemory<byte> payload, long timestamp)
    {
        var validation = new CommandValidator().Validate(payload);
        // 깨진/없는/중복된 ID를 임의로 만들어 응답하지 않습니다.
        if (validation.CommandId is null) return new(null, validation.Error);
        // 이동·상태 시뮬레이션은 다음 과제입니다. 수신했다는 이유로 성공 처리하지 않습니다.
        var reason = validation.IsValid ? "SIMULATION_NOT_IMPLEMENTED"
            : $"{validation.Error!.Code} {validation.Error.Field}: {validation.Error.Reason}";
        var ack = JsonSerializer.Serialize(new
        {
            id = amrId, cmdId = validation.CommandId, ts = timestamp, result = "FAILURE", reason
        });
        return new(ack, validation.Error);
    }
}
