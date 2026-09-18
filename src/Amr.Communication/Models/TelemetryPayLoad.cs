using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Amr.Communication.Models
{
    // 로봇에서 수신되는 불변(Immutable) 페이로드 레코드
    public record TelemetryPayload(
        [property: JsonPropertyName("ts")] long Timestamp,
        [property: JsonPropertyName("Pos")] PositionData Position
    );

    // 내부 좌표 데이터 역시 불변 레코드로 처리
    public record PositionData(
        [property: JsonPropertyName("x")] double X,
        [property: JsonPropertyName("y")] double Y,
        [property: JsonPropertyName("sp")] double Speed,
        [property: JsonPropertyName("stat")] string Status
    );
}
