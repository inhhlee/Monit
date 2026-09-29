using System.Linq;
using System.Buffers;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Protocol;
using Amr.Communication.Mqtt;
using Amr.TestClient;

// 첫 실습: 가상 AMR 한 대가 로컬 Broker에 Heartbeat를 보냅니다.
string amrId = args.Length > 0 ? args[0] : "AMR_001";
const string host = "127.0.0.1";
const int port = 1883;
var topic = $"amr/{amrId}/heartbeat";
var commandTopic = MqttTopics.Target.Command(amrId);

using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    stop.Cancel();
};

using var client = new MqttClientFactory().CreateMqttClient();
var options = new MqttClientOptionsBuilder()
    .WithTcpServer(host, port)
    .WithClientId($"virtual-{amrId}-{Guid.NewGuid():N}")
    .WithProtocolVersion(MqttProtocolVersion.V500)
    .WithCleanStart()
    .WithKeepAlivePeriod(TimeSpan.FromSeconds(5))
    .WithTimeout(TimeSpan.FromSeconds(3))
    .Build();

var exitCode = 0;
try
{
    // 1. Broker에 연결합니다.
    var result = await client.ConnectAsync(options, stop.Token);
    if (result.ResultCode != MqttClientConnectResultCode.Success)
        throw new InvalidOperationException($"연결 거부: {result.ResultCode}");
    Console.WriteLine($"Connected: {host}:{port} / {amrId} (종료: Ctrl+C)");

    client.ApplicationMessageReceivedAsync += async e =>
    {
        var receivedTopic = e.ApplicationMessage.Topic;
        var payloadString = e.ApplicationMessage.ConvertPayloadToString();
        Console.WriteLine($"Received Command: {receivedTopic} {payloadString}");

        if (receivedTopic != commandTopic) return;
        var response = BasicCommandHandler.Handle(amrId, e.ApplicationMessage.Payload.ToArray(),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        if (response.Error is { } error)
            Console.WriteLine($"Rejected Command: {error.Code} {error.Field}: {error.Reason}");
        if (response.AckPayload is not { } ackPayload) return;

        var ackTopic = $"amr/{amrId}/ack";
        var ackMessage = new MqttApplicationMessageBuilder()
            .WithTopic(ackTopic)
            .WithPayload(ackPayload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
            .Build();

        await client.PublishAsync(ackMessage, stop.Token);
        Console.WriteLine($"Published ACK: {ackTopic} {ackPayload}");
    };

    var subOptions = new MqttClientSubscribeOptionsBuilder()
        .WithTopicFilter(f => f.WithTopic(commandTopic).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce))
        .Build();
    await client.SubscribeAsync(subOptions, stop.Token);
    Console.WriteLine($"Subscribed to commands: {commandTopic}");
    while (!stop.IsCancellationRequested)
    {
        // 2. 계약 v2: AMR ID, 현재 시각(Unix 밀리초), 생존 상태를 JSON으로 만듭니다.
        var payload = JsonSerializer.Serialize(new
        {
            id = amrId,
            ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            status = "OK"
        });
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
            .WithRetainFlag(false)
            .Build();

        // 3. 발행하고 1초 기다립니다. 수신 여부는 구독 측에서 확인합니다.
        await client.PublishAsync(message, stop.Token);
        Console.WriteLine($"Published: {topic} {payload}");
        await Task.Delay(TimeSpan.FromSeconds(1), stop.Token);
    }
}
catch (OperationCanceledException) when (stop.IsCancellationRequested)
{
    // Ctrl+C로 요청한 정상 종료입니다.
}
catch (Exception error)
{
    Console.Error.WriteLine($"통신 실패: {error.Message}");
    Console.Error.WriteLine("Broker 실행 상태를 확인한 후 다시 실행하세요.");
    exitCode = 1;
}

// 4. 종료할 때 연결을 정리합니다.
if (client.IsConnected)
{
    try
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await client.DisconnectAsync(new MqttClientDisconnectOptions(), timeout.Token);
    }
    catch (Exception error)
    {
        Console.Error.WriteLine($"연결 종료 실패: {error.Message}");
        exitCode = 1;
    }
}
Console.WriteLine("가상 AMR 종료");
return exitCode;
