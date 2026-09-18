namespace Amr.Communication.Mqtt;

/// <summary>Development TCP settings. AMR heartbeat policy is independent.</summary>
public sealed class BrokerConnectionOptions
{
    public string Host { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 1883;
    public string ClientId { get; init; } = $"amr-control-{Guid.NewGuid():N}";
    public TimeSpan KeepAlive { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(3);
    public string? Username { get; init; }
    public string? Password { get; init; }

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Host);
        ArgumentException.ThrowIfNullOrWhiteSpace(ClientId);
        if (Port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(Port));
        if (KeepAlive.TotalSeconds is < 1 or > 65535 || KeepAlive.TotalSeconds != Math.Truncate(KeepAlive.TotalSeconds))
            throw new ArgumentOutOfRangeException(nameof(KeepAlive), "KeepAlive must be whole seconds from 1 to 65535.");
        if (Timeout <= TimeSpan.Zero || Timeout > TimeSpan.FromMinutes(5))
            throw new ArgumentOutOfRangeException(nameof(Timeout));
        if (Password is not null && string.IsNullOrWhiteSpace(Username))
            throw new ArgumentException("Username is required when a password is configured.");
    }
}
