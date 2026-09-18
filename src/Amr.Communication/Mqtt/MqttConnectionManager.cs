using System.Net.Sockets;

namespace Amr.Communication.Mqtt;

/// <summary>
/// Serializes explicit lifecycle requests and isolates late callbacks from retired sessions.
/// No automatic reconnect. Event handlers must be brief and must not synchronously wait
/// on lifecycle methods. Observer failures cannot interrupt connection processing.
/// </summary>
public sealed class MqttConnectionManager : IAsyncDisposable
{
    private readonly BrokerConnectionOptions _options;
    private readonly Func<IBrokerClient> _createClient;
    private readonly SemaphoreSlim _operation = new(1, 1);
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
    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        try { await _operation.WaitAsync(cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { return false; }
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            lock (_stateLock)
            {
                if (_client?.IsConnected == true && _snapshot.State == BrokerConnectionState.Connected) return true;
                RetireClient();
                SetState(BrokerConnectionState.Connecting, "ConnectRequested");
            }

            try
            {
                var client = _createClient();
                client.Disconnected += args => OnDisconnected(client, args);
                lock (_stateLock) _client = client;
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(_options.Timeout);
                await client.ConnectAsync(_options, timeout.Token).ConfigureAwait(false);
                lock (_stateLock)
                {
                    // A disconnect can arrive before ConnectAsync returns.
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
