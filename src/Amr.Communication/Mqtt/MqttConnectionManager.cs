using System.Net.Sockets;

namespace Amr.Communication.Mqtt;

/// <summary>
/// Serializes explicit lifecycle requests and isolates late callbacks from retired sessions.
/// No automatic reconnect. Event handlers must be brief and must not synchronously wait
/// on lifecycle methods. Observer failures cannot interrupt connection processing. 
/// </summary>
/// 

// mqtt 연결 상태 감시 
public sealed class MqttConnectionManager : IAsyncDisposable // mqtt관리 매니저 
{
    private readonly BrokerConnectionOptions _options; //
    private readonly Func<IBrokerClient> _createClient; // 추상 함수를 관리하는 Broker 클라이언트 생성 
    private readonly SemaphoreSlim _operation = new(1, 1); // 세마포어(비동기) 동작 ( 동시에 최대 1개)
    private readonly object _stateLock = new();
    private IBrokerClient? _client;
    private bool _disposed;
    private BrokerConnectionSnapshot _snapshot = NewSnapshot(BrokerConnectionState.Disconnected, "Initial");

    public MqttConnectionManager(BrokerConnectionOptions options) : this(options, () => new MqttClientService()) { }

    internal MqttConnectionManager(BrokerConnectionOptions options, Func<IBrokerClient> createClient)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _options = options;
        _createClient = createClient;
    }

    public event Action<BrokerConnectionSnapshot>? StateChanged;
    public BrokerConnectionSnapshot Snapshot { get { lock (_stateLock) return _snapshot; } }
    public bool IsConnected => Snapshot.State == BrokerConnectionState.Connected;

    /// <returns>False for a connection failure or cancellation; details are in Snapshot.</returns>
    /// 

    ///  핵심 제어 비동기 코드 
    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        try { await _operation.WaitAsync(cancellationToken).ConfigureAwait(false); } // 대기 도중 연결 취소면 반환
        catch (OperationCanceledException) { return false; }


        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this); // 프로그램이 종료 되어 접근 제한 
            lock (_stateLock)
            {
                if (_client?.IsConnected == true && _snapshot.State == BrokerConnectionState.Connected) return true; // 연결 되어 있거나 현재 state가 같으면 반환
                RetireClient(); 
                SetState(BrokerConnectionState.Connecting, "ConnectRequested");
            }

            try
            {
                var client = _createClient(); // 새로운 client 생성 
                client.Disconnected += args => OnDisconnected(client, args);  // 클라이언트 연결이 끊기면 함수로 반환
                lock (_stateLock) _client = client; // 현재 실행 중인 스레드가 


                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); // (사용자 취소버튼, 네트워크 에러) => 취소 2개 토큰 묶어서 하나로 만듬
                timeout.CancelAfter(_options.Timeout); // 연결/비연결 작업 대기 시간


                await client.ConnectAsync(_options, timeout.Token).ConfigureAwait(false);  // 실제 브로커 서버 연결 ( 요 부분 다시 공부 ) => 정상 연결 성공 
                lock (_stateLock)
                {
                    // A disconnect can arrive before ConnectAsync returns. 중요
                    if (!client.IsConnected || _snapshot.State == BrokerConnectionState.Faulted)
                    {
                        RetireClient();
                        if (_snapshot.State != BrokerConnectionState.Faulted)
                            SetState(BrokerConnectionState.Faulted, "ConnectionLost", "Transport closed during connect");
                        return false;
                    }
                    SetState(BrokerConnectionState.Connected, "Connected");
                    return true;
                }
            }
            catch (Exception error)
            {
                lock (_stateLock)
                {
                    RetireClient();
                    var cancelled = cancellationToken.IsCancellationRequested;
                    SetState(cancelled ? BrokerConnectionState.Disconnected : BrokerConnectionState.Faulted,
                        cancelled ? "ConnectCancelled" : "ConnectFailed", Describe(error));
                }
                return false;
            }
        }
        finally { _operation.Release(); }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _operation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            await DisconnectCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally { _operation.Release(); }
    }

    // 이벤트로 DTO 변환 
    private async Task DisconnectCoreAsync(CancellationToken cancellationToken)
    {
        IBrokerClient? client;
        lock (_stateLock)
        {
            client = _client;
            // Ignore intentional-close callbacks, including those dispatched after a retry.
            _client = null;
            if (client is null)
            {
                SetState(BrokerConnectionState.Disconnected, "DisconnectRequested");
                return;
            }
            SetState(BrokerConnectionState.Disconnecting, "DisconnectRequested");
        }
        string? failure = null;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_options.Timeout);
            if (client.IsConnected) await client.DisconnectAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (Exception error) { failure = Describe(error); }
        finally
        {
            client.Dispose();
            lock (_stateLock)
                SetState(BrokerConnectionState.Disconnected,
                    failure is null ? "DisconnectCompleted" : "DisconnectCleanup", failure);
        }
    }

    // 연결 관리 끊김 확인( Broker와의 연결() 
    private void OnDisconnected(IBrokerClient source, BrokerDisconnected args)
    {
        lock (_stateLock)
        {
            if (_disposed || !ReferenceEquals(source, _client) || !args.WasConnected) return;
            if (_snapshot.State is not (BrokerConnectionState.Connecting or BrokerConnectionState.Connected)) return;
            SetState(BrokerConnectionState.Faulted, "ConnectionLost",
                args.Exception is null ? args.Reason : $"{args.Reason}: {Describe(args.Exception)}");
        }
    }

    // Called under _stateLock; clear identity before disposing a transport.
    private void RetireClient()
    {
        var old = _client;
        _client = null;
        old?.Dispose();
    }

    private void SetState(BrokerConnectionState state, string reason, string? error = null)
    {
        _snapshot = NewSnapshot(state, reason, error);
        if (StateChanged is not { } observers) return;
        foreach (Action<BrokerConnectionSnapshot> observer in observers.GetInvocationList())
        {
            try { observer(_snapshot); }
            catch { /* Observers must not break the MQTT receive loop or lifecycle. */ }
        }
    }

    private static BrokerConnectionSnapshot NewSnapshot(BrokerConnectionState state, string reason, string? error = null) =>
        new(state, reason, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), error);

    private static string Describe(Exception error)
    {
        // Avoid printing user credentials or server-controlled free text in connection logs.
        var parts = new List<string>();
        for (Exception? current = error; current is not null; current = current.InnerException)
            parts.Add(current is SocketException socket ? $"SocketException({socket.SocketErrorCode})" : current.GetType().Name);
        return string.Join(" -> ", parts);
    }

    public async ValueTask DisposeAsync()
    {
        await _operation.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            await DisconnectCoreAsync(CancellationToken.None).ConfigureAwait(false);
            lock (_stateLock)
            {
                _disposed = true;
                SetState(BrokerConnectionState.Disposed, "Disposed");
            }
        }
        finally { _operation.Release(); }
    }
}
