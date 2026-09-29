namespace Amr.Communication.Mqtt;

internal sealed record BrokerDisconnected(bool WasConnected, string Reason, Exception? Exception = null); // (이전에 실제 연결 상태 확인, 왜 끊어 졌는지 ,관련 예외) 
/// <summary>Transport seam for deterministic lifecycle tests; production uses MQTTnet.</summary> // Test 넣어서 진행 할 수 있음 
internal interface IBrokerClient : IDisposable //   MQTT Client 추상화 인터페이스 동작 모음 
{
    bool IsConnected { get; } // 상태확인
    event Action<BrokerDisconnected>? Disconnected;  // 구독 기능 
    Task ConnectAsync(BrokerConnectionOptions options, CancellationToken cancellationToken);  // 연결하기 (비동기 메서드 (접속에 필요한 정보 객체 , 비동기 처리( timeout 시 중단 요청))) 
    Task DisconnectAsync(CancellationToken cancellationToken);  // 연결끊기 (비동기 메서드(신호 들어오면  동작)
    Task PublishCommandAsync(string amrId, string payload, CancellationToken cancellationToken = default); // Command 발행
}
