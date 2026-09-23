using MQTTnet;
using MQTTnet.Formatter;
using Amr.Communication.Validation;

namespace Amr.Communication.Mqtt;

/// <summary>One MQTT transport session. Ownership belongs to MqttConnectionManager.</summary> 실제 Broker 연결 기능 담당
public sealed class MqttClientService : IBrokerClient // 상속 금지
{
    private readonly IMqttClient _client = new MqttClientFactory().CreateMqttClient(); // 캡슐화 
    private readonly MqttSubscriber? _subscriber;

    public MqttClientService() => _client.DisconnectedAsync += OnDisconnectedAsync; // 생성자 끊김 이벤트 발생시 OnDis 함수 호출 

    internal MqttClientService(IncomingMessageProcessor processor) : this()
        => _subscriber = new MqttSubscriber(_client, processor);
    public bool IsConnected => _client.IsConnected;
    private event Action<BrokerDisconnected>? Disconnected;
    event Action<BrokerDisconnected>? IBrokerClient.Disconnected
    {
        add => Disconnected += value;
        remove => Disconnected -= value;
    }

    public async Task ConnectAsync(BrokerConnectionOptions options, CancellationToken cancellationToken = default)  // 실제 Broker 연결 수행 (Builder 패턴 사용) -> 비동기 작업 후 반환 값 x 
    {
        options.Validate(); // broker 규칙 검증 
        var builder = new MqttClientOptionsBuilder() // 객체 생성
            .WithTcpServer(options.Host, options.Port) // Mosquitto 위치(Host-port)
            .WithClientId(options.ClientId) // id
            .WithProtocolVersion(MqttProtocolVersion.V500) //mqtt version
            .WithCleanStart() // 새 mqtt 세션(???)
            .WithKeepAlivePeriod(options.KeepAlive)
            .WithTimeout(options.Timeout);

        if (options.Username is not null) builder.WithCredentials(options.Username, options.Password);

        var result = await _client.ConnectAsync(builder.Build(), cancellationToken).ConfigureAwait(false); //  비동기 함수 

        if (result.ResultCode != MqttClientConnectResultCode.Success)
            throw new InvalidOperationException($"Broker rejected connection: {result.ResultCode}");

        // 관제 수신 경로가 있으면 연결한 클라이언트로 Heartbeat를 구독합니다.
        if (_subscriber is not null)
            await _subscriber.SubscribeAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default) => // 능동적 연결 종료
        _client.DisconnectAsync(new MqttClientDisconnectOptions(), cancellationToken);

    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args)  // 끊김 알림 수신 및 통역 
    {
        Disconnected?.Invoke(new(args.ClientWasConnected, args.Reason.ToString(), args.Exception)); // 누군가 사용중이면 pass 
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _subscriber?.Dispose();
        _client.DisconnectedAsync -= OnDisconnectedAsync;
        _client.Dispose();
    }
}
