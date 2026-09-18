using MQTTnet;
using MQTTnet.Formatter;

namespace Amr.Communication.Mqtt;

/// <summary>One MQTT transport session. Ownership belongs to MqttConnectionManager.</summary>
public sealed class MqttClientService : IBrokerClient
{
    private readonly IMqttClient _client = new MqttClientFactory().CreateMqttClient();

    public MqttClientService() => _client.DisconnectedAsync += OnDisconnectedAsync;
    public bool IsConnected => _client.IsConnected;
    private event Action<BrokerDisconnected>? Disconnected;
    event Action<BrokerDisconnected>? IBrokerClient.Disconnected
    {
        add => Disconnected += value;
        remove => Disconnected -= value;
    }

    public async Task ConnectAsync(BrokerConnectionOptions options, CancellationToken cancellationToken = default)
    {
        options.Validate();
        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(options.Host, options.Port)
            .WithClientId(options.ClientId)
            .WithProtocolVersion(MqttProtocolVersion.V500)
            .WithCleanStart()
            .WithKeepAlivePeriod(options.KeepAlive)
            .WithTimeout(options.Timeout);
        if (options.Username is not null) builder.WithCredentials(options.Username, options.Password);
        var result = await _client.ConnectAsync(builder.Build(), cancellationToken).ConfigureAwait(false);
        if (result.ResultCode != MqttClientConnectResultCode.Success)
            throw new InvalidOperationException($"Broker rejected connection: {result.ResultCode}");
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default) =>
        _client.DisconnectAsync(new MqttClientDisconnectOptions(), cancellationToken);

    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args)
    {
        Disconnected?.Invoke(new(args.ClientWasConnected, args.Reason.ToString(), args.Exception));
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _client.DisconnectedAsync -= OnDisconnectedAsync;
        _client.Dispose();
    }
}
