using Amr.Communication.Mqtt;
using System;
using System.Buffers;
using Amr.Communication.Validation;
using Amr.Communication.Monitoring;
using MQTTnet;
using MQTTnet.Protocol;

namespace Amr.Communication.Mqtt;

// 택배 접수처에 비유하면, Broker가 배달한 네 종류의 메시지를 받아 분류 담당자에게 넘기는 클래스입니다.
// internal: 이 프로젝트 안에서 사용합니다. sealed: 이 클래스를 상속해 바꾸지는 않습니다.
// IDisposable: 사용이 끝나면 메시지 수신 등록을 해제할 수 있다는 약속입니다.
internal sealed class MqttSubscriber : IDisposable
{
    // Broker와 통신하는 실제 MQTT 연결입니다. readonly는 생성 후 이 필드의 참조를 바꾸지 않는다는 뜻입니다.
    private readonly IMqttClient _client;
    // 메시지를 검사하고 AMR별 보관함(큐)에 분류하는 담당자입니다.
    private readonly AmrMessageDispatcher _dispatcher;

    // 생성자: 연결(client)과 검사/전달 담당자(processor)를 받아 접수처를 준비합니다.
    public MqttSubscriber(IMqttClient client, IncomingMessageProcessor processor,
        HeartbeatMonitorOptions? heartbeatOptions = null, IHeartbeatEventSink? heartbeatSink = null)
    {
        // 받은 MQTT 연결을 나중에도 사용할 수 있도록 필드에 저장합니다.
        _client = client;
        // 이 연결에서 사용할 AMR별 대기열 관리자를 만듭니다. 아직 AMR별 작업은 시작하지 않습니다.
        var monitor = heartbeatOptions is null ? null : new HeartbeatMonitor(heartbeatOptions, heartbeatSink!);
        _dispatcher = new AmrMessageDispatcher(processor, heartbeats: monitor);
        // +=는 알림 등록입니다. 실제 메시지가 도착하면 아래 OnMessageReceivedAsync를 호출하게 합니다.
        _client.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
    }

    // Broker에 받을 메시지 종류를 신청합니다. cancellationToken은 도중에 그만하라는 신호입니다.
    public async Task SubscribeAsync(CancellationToken cancellationToken)
    {
        // MQTT 구독 신청서를 만들 도구를 준비합니다.
        var factory = new MqttClientFactory();
        // Builder는 신청서의 항목을 한 줄씩 채우는 도구입니다.
        var options = factory.CreateSubscribeOptionsBuilder()
            // 모든 AMR의 생존 신호를 신청합니다. +는 AMR ID 한 칸에 어떤 값이 와도 받는다는 뜻입니다.
            .WithTopicFilter(f => f.WithTopic(MqttTopics.Subscribe.AllHeartbeat).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce))
            // 위치·속도·상태 메시지도 신청합니다. AtMostOnce는 전달 재시도를 보장하지 않는 QoS 0입니다.
            .WithTopicFilter(f => f.WithTopic(MqttTopics.Subscribe.AllTelemetry).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce))
            // 알람 메시지를 신청합니다.
            .WithTopicFilter(f => f.WithTopic(MqttTopics.Subscribe.AllAlarm).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce))
            // 명령에 대한 응답(ACK)을 신청합니다.
            .WithTopicFilter(f => f.WithTopic(MqttTopics.Subscribe.AllAck).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce))
            // 작성한 내용을 실제 구독 설정으로 완성합니다.
            .Build();
        // Broker의 구독 응답을 기다립니다. await는 미완료 작업을 기다리는 동안 스레드를 붙잡지 않습니다.
        // ConfigureAwait(false)는 이어질 작업이 원래 실행 문맥으로 돌아올 필요가 없다는 뜻입니다.
        var result = await _client.SubscribeAsync(options, cancellationToken).ConfigureAwait(false);
        // 네 신청 중 하나라도 QoS 0으로 승인되지 않았다면 실패로 판단합니다.
        if (result.Items.Any(item => item.ResultCode != MqttClientSubscribeResultCode.GrantedQoS0))
            // 조용히 넘어가지 않고 연결을 준비하는 쪽에 실패를 알립니다.
            throw new InvalidOperationException("Broker rejected one or more subscriptions.");
    }

    // 수신된 메시지를 처리하고, 검증을 통과하면 Core 경계의 수신자에게 전달합니다
    // MQTT 메시지를 받아 넘김
    // [1] 접수: args는 MQTTnet이 넘겨준 배달 봉투이며, Topic(주소)과 Payload(내용)가 들어 있습니다.
    // Task를 반환하지만 여기서는 await하지 않습니다. 접수 후 오래 걸리는 처리는 별도 작업에 맡깁니다.
    private Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        // 검사를 통과해야 Core 경계의 수신자(현재는 콘솔 출력 대역)가 호출됩니다.
        // ToArray()는 수신 버퍼의 내용을 복사해 이 콜백이 끝난 뒤에도 안전하게 검사·처리하도록 합니다.
        // TryEnqueue는 지금 검사하고 AMR별 큐에 넣기만 합니다. 거부 사유의 기록도 내부에서 처리합니다.
        _dispatcher.TryEnqueue(args.ApplicationMessage.Topic, args.ApplicationMessage.Payload.ToArray());
        // "접수 콜백이 끝났다"는 뜻입니다. "Core 처리까지 끝났다"거나 "AMR에 ACK를 보냈다"는 뜻은 아닙니다.
        return Task.CompletedTask;
    }

    // 정상 종료: 새 배달 알림을 끊고, 이미 시작한 처리 작업들의 종료를 기다릴 수 있게 합니다.
    public Task StopAsync(CancellationToken cancellationToken)
    {
        // -=는 알림 해제입니다. 이미 실행에 들어간 콜백은 있을 수 있어 dispatcher도 종료 여부를 검사합니다.
        _client.ApplicationMessageReceivedAsync -= OnMessageReceivedAsync;
        // 종료 Task를 호출자에게 돌려줍니다. 호출자는 await로 기다리고 토큰으로 대기를 취소할 수 있습니다.
        return _dispatcher.StopAsync(cancellationToken);
    }

    // 동기 정리: 기다릴 수 없는 호출 경로에서는 종료를 요청하고 바로 돌아갑니다.
    public void Dispose()
    {
        // 더 이상 새 수신 알림을 받지 않습니다.
        _client.ApplicationMessageReceivedAsync -= OnMessageReceivedAsync;
        // 작업 취소와 남은 큐 정리를 요청합니다. 실제 정리 완료를 여기서 기다리지는 않습니다.
        _dispatcher.Dispose();
    }
}
