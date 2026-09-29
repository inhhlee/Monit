using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Protocol;

namespace Amr.Communication.Mqtt;

// 관제 시스템이 AMR로 Command를 발행하는 역할
public class MqttPublisher
{
    private readonly IMqttClient _client;

    public MqttPublisher(IMqttClient client)
    {
        _client = client;
    }

    public async Task PublishCommandAsync(string amrId, string payload, CancellationToken cancellationToken = default)
    {
        var topic = MqttTopics.Target.Command(amrId);
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce) // 현재는 QoS 0 적용
            .WithRetainFlag(false)
            .Build();

        await _client.PublishAsync(message, cancellationToken).ConfigureAwait(false);
    }
}
