using Amr.Communication.Mqtt;
using System;
using System.Buffers;
using Amr.Communication.Validation;
using MQTTnet;
using MQTTnet.Protocol;

namespace Amr.Communication.Mqtt;

// 첫 수신 단계: Heartbeat를 구독하고 받은 바이트를 기존 검사기로 전달합니다.
internal sealed class MqttSubscriber : IDisposable
{
    private readonly IMqttClient _client;
    private readonly IncomingMessageProcessor _processor;

    public MqttSubscriber(IMqttClient client, IncomingMessageProcessor processor)
    {
        _client = client;
        _processor = processor;
        _client.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
    }

    public async Task SubscribeAsync(CancellationToken cancellationToken)
    {
        var options = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(filter => filter.WithTopic(MqttTopics.Subscribe.AllHeartbeat)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce))
            .Build();
        var result = await _client.SubscribeAsync(options, cancellationToken).ConfigureAwait(false);
        if (result.Items.Count != 1 || result.Items.First().ResultCode != MqttClientSubscribeResultCode.GrantedQoS0)
            throw new InvalidOperationException("Broker rejected the Heartbeat subscription.");
    }

    private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        // 검사를 통과해야 Core 경계의 수신자(현재는 콘솔 출력 대역)가 호출됩니다.
        await _processor.ProcessAsync(args.ApplicationMessage.Topic,
            args.ApplicationMessage.Payload.ToArray()).ConfigureAwait(false);
    }

    public void Dispose() => _client.ApplicationMessageReceivedAsync -= OnMessageReceivedAsync;
}
