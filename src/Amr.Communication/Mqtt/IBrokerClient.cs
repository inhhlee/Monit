namespace Amr.Communication.Mqtt;

internal sealed record BrokerDisconnected(bool WasConnected, string Reason, Exception? Exception = null);

/// <summary>Transport seam for deterministic lifecycle tests; production uses MQTTnet.</summary>
internal interface IBrokerClient : IDisposable
{
    bool IsConnected { get; }
    event Action<BrokerDisconnected>? Disconnected;
    Task ConnectAsync(BrokerConnectionOptions options, CancellationToken cancellationToken);
    Task DisconnectAsync(CancellationToken cancellationToken);
}
