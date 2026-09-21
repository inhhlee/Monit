namespace Amr.Communication.Mqtt;

/// <summary>Development TCP settings. AMR heartbeat policy is independent.</summary> // 개발 TCP 설정, AMR 하트비트 정책은 독립적 (KeepAlive와 AMR Hearbeat 다름) 
public sealed class BrokerConnectionOptions
{
    public string Host { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 1883;
    public string ClientId { get; init; } = $"amr-control-{Guid.NewGuid():N}";
    public TimeSpan KeepAlive { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(3);
    public string? Username { get; init; }
    public string? Password { get; init; }

    internal void Validate() // Broker 검증
    {
        // 문자열 WhiteSpace 찾기
        ArgumentException.ThrowIfNullOrWhiteSpace(Host); 
        ArgumentException.ThrowIfNullOrWhiteSpace(ClientId); 

        if (Port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(Port)); // nameof로 문자열로 대체 

        if (KeepAlive.TotalSeconds is < 1 or > 65535 || KeepAlive.TotalSeconds != Math.Truncate(KeepAlive.TotalSeconds)) // 포트 번호 최소, 최대 범위 || 정수단위 에러 체크 
            throw new ArgumentOutOfRangeException(nameof(KeepAlive), "KeepAlive must be whole seconds from 1 to 65535.");

        if (Timeout <= TimeSpan.Zero || Timeout > TimeSpan.FromMinutes(5)) // 시간초과 
            throw new ArgumentOutOfRangeException(nameof(Timeout));

        if (Password is not null && string.IsNullOrWhiteSpace(Username)) // id,pw 둘 중 하나 에러 시 
            throw new ArgumentException("Username is required when a password is configured.");
    }
}
