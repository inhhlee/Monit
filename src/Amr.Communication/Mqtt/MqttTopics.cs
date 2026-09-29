using System;

namespace Amr.Communication.Mqtt
{
    public static class MqttTopics // Topic 문자열을 한곳에서 관리하여 파일로 라이브러리로 관리하여 
    {
        private const string BasePrefix = "amr"; // amr로 Topic의 구조를 정해서 사용하기

        // 구독(수신)용 와일드카드 토픽들
        public static class Subscribe
        {
            public static string AllTelemetry => $"{BasePrefix}/+/telemetry";
            public static string AllAlarm => $"{BasePrefix}/+/alarm";
            public static string AllHeartbeat => $"{BasePrefix}/+/heartbeat";
            public static string AllAck => $"{BasePrefix}/+/ack";
        }

        // 특정 로봇 발행(송신)용 토픽들
        public static class Target
        {
            public static string Command(string amrId)
            {
                ValidateAmrId(amrId);
                return $"{BasePrefix}/{amrId}/command";
            }
        }

        private static void ValidateAmrId(string amrId)
        {
            if (string.IsNullOrWhiteSpace(amrId) ||
                amrId.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c is '/' or '+' or '#'))
            {
                throw new ArgumentException("AMR ID는 공백이나 Topic 구분자/와일드카드를 포함할 수 없습니다.", nameof(amrId));
            }
        }
    }
}
