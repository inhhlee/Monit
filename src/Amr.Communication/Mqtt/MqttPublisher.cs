using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Amr.Communication.Validation;
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
        ArgumentNullException.ThrowIfNull(payload);
        byte[] bytes;
        try { bytes = new UTF8Encoding(false, true).GetBytes(payload); }
        catch (EncoderFallbackException error) { throw new ArgumentException("Invalid Unicode in Command.", nameof(payload), error); }
        var validation = new CommandValidator().Validate(bytes);
        if (!validation.IsValid)
            throw new ArgumentException($"Invalid Command: {validation.Error!.Code} {validation.Error.Field}: {validation.Error.Reason}", nameof(payload));
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(bytes)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce) // 현재는 QoS 0 적용
            .WithRetainFlag(false)
            .Build();

        await _client.PublishAsync(message, cancellationToken).ConfigureAwait(false);
    }
}
