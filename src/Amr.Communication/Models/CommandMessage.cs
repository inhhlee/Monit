using System;
using System.Collections.Generic;
using System.Text;

namespace Amr.Communication.Models
{
    public enum CommandType { Move, Stop, Reset }
    public sealed record CommandPosition(double X, double Y);

    // ID는 요청 측에서 생성하며 통신 계층과 AMR은 받은 값을 보존합니다.
    public sealed record CommandMessage(string CommandId, long TimestampUnixMilliseconds,
        CommandType Type, CommandPosition? Position);
}
