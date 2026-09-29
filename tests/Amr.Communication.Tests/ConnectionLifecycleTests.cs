using Amr.Communication.Mqtt;

namespace Amr.Communication.Tests;

public class ConnectionLifecycleTests
{
    private static BrokerConnectionOptions Options(TimeSpan? timeout = null) => new() { Timeout = timeout ?? TimeSpan.FromSeconds(3) };

    [Fact]
    public async Task SuccessAndExplicitDisconnectReportStateAndDisposeTransport()
    {
        var client = new FakeClient();
        await using var manager = new MqttConnectionManager(Options(), () => client);
        var states = new List<BrokerConnectionSnapshot>();
        manager.StateChanged += states.Add;
        Assert.True(await manager.ConnectAsync());
        Assert.True(manager.IsConnected);
        Assert.Equal(TimeSpan.FromSeconds(5), client.Options!.KeepAlive);
        await manager.DisconnectAsync();
        Assert.False(manager.IsConnected);
        Assert.True(client.Disposed);
        Assert.Equal(new[] { BrokerConnectionState.Connecting, BrokerConnectionState.Connected,
            BrokerConnectionState.Disconnecting, BrokerConnectionState.Disconnected }, states.Select(x => x.State));
        Assert.All(states, x => Assert.True(x.TimestampUnixMilliseconds > int.MaxValue));
    }

    [Fact]
    public async Task FailedConnectionAndLateCallbackCannotFaultExplicitRetry()
    {
        var failed = new FakeClient { Connect = _ => throw new IOException("fixture failure") };
        var clients = new Queue<FakeClient>([failed, new()]);
        await using var manager = new MqttConnectionManager(Options(), () => clients.Dequeue());
        Assert.False(await manager.ConnectAsync());
        Assert.Equal(BrokerConnectionState.Faulted, manager.Snapshot.State);
        Assert.Equal("ConnectFailed", manager.Snapshot.Reason);
        Assert.Contains("IOException", manager.Snapshot.Error);
        Assert.True(failed.Disposed);
        Assert.True(await manager.ConnectAsync());
        failed.Emit(new(true, "Late disconnect"));
        Assert.True(manager.IsConnected);
    }

    [Fact]
    public async Task UnexpectedDisconnectFaultsOnceWithoutAutomaticReconnect()
    {
        var client = new FakeClient();
        var creations = 0;
        await using var manager = new MqttConnectionManager(Options(), () => { creations++; return client; });
        var events = new List<BrokerConnectionSnapshot>();
        manager.StateChanged += events.Add;
        Assert.True(await manager.ConnectAsync());
        client.Drop();
        client.Emit(new(true, "Duplicate"));
        Assert.False(manager.IsConnected);
        Assert.Equal("ConnectionLost", manager.Snapshot.Reason);
        Assert.Single(events, x => x.Reason == "ConnectionLost");
        Assert.Equal(1, creations);
    }

    [Fact]
    public async Task TimeoutReturnsFailureAndReleasesResources()
    {
        var client = new FakeClient { Connect = token => Task.Delay(Timeout.Infinite, token) };
        await using var manager = new MqttConnectionManager(Options(TimeSpan.FromMilliseconds(50)), () => client);
        Assert.False(await manager.ConnectAsync().WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal("ConnectFailed", manager.Snapshot.Reason);
        Assert.True(client.Disposed);
    }

    [Fact]
    public async Task CallerCancellationIsDistinctFromFailure()
    {
        using var cancel = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new FakeClient { Connect = token => { started.SetResult(); return Task.Delay(Timeout.Infinite, token); } };
        await using var manager = new MqttConnectionManager(Options(), () => client);
        var connect = manager.ConnectAsync(cancel.Token);
        await started.Task;
        cancel.Cancel();
        Assert.False(await connect);
        Assert.Equal("ConnectCancelled", manager.Snapshot.Reason);
        Assert.Equal(BrokerConnectionState.Disconnected, manager.Snapshot.State);
        Assert.True(client.Disposed);
    }

    [Fact]
    public async Task ConcurrentConnectsDoNotDuplicateSessions()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new FakeClient { Connect = _ => release.Task };
        var creations = 0;
        await using var manager = new MqttConnectionManager(Options(), () => { creations++; return client; });
        var first = manager.ConnectAsync();
        var second = manager.ConnectAsync();
        release.SetResult();
        Assert.All(await Task.WhenAll(first, second), result => Assert.True(result));
        Assert.Equal(1, creations);
    }

    [Fact]
    public async Task DisconnectDuringConnectWaitsThenClosesSession()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new FakeClient { Connect = _ => release.Task };
        await using var manager = new MqttConnectionManager(Options(), () => client);
        var connect = manager.ConnectAsync();
        var disconnect = manager.DisconnectAsync();
        release.SetResult();
        Assert.True(await connect);
        await disconnect;
        Assert.False(manager.IsConnected);
        Assert.True(client.Disposed);
    }

    [Fact]
    public async Task DisconnectBeforeConnectReturnsCannotPublishConnectedState()
    {
        var client = new FakeClient { DropBeforeReturn = true };
        await using var manager = new MqttConnectionManager(Options(), () => client);
        var events = new List<BrokerConnectionSnapshot>();
        manager.StateChanged += events.Add;
        Assert.False(await manager.ConnectAsync());
        Assert.DoesNotContain(events, x => x.State == BrokerConnectionState.Connected);
        Assert.Equal("ConnectionLost", manager.Snapshot.Reason);
    }

    [Fact]
    public async Task FailingObserverCannotBreakLifecycleOrOtherObservers()
    {
        await using var manager = new MqttConnectionManager(Options(), () => new FakeClient());
        manager.StateChanged += _ => throw new InvalidOperationException("observer");
        var events = new List<BrokerConnectionSnapshot>();
        manager.StateChanged += events.Add;
        Assert.True(await manager.ConnectAsync());
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public async Task DisconnectFailureStillClosesTransportAndDisposeIsIdempotent()
    {
        var client = new FakeClient { Disconnect = _ => throw new IOException("close failure") };
        var manager = new MqttConnectionManager(Options(), () => client);
        Assert.True(await manager.ConnectAsync());
        await manager.DisconnectAsync();
        Assert.Equal("DisconnectCleanup", manager.Snapshot.Reason);
        Assert.True(client.Disposed);
        await manager.DisposeAsync();
        await manager.DisposeAsync();
        Assert.Equal(BrokerConnectionState.Disposed, manager.Snapshot.State);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => manager.ConnectAsync());
    }

    [Theory]
    [InlineData(0, 5, 3)]
    [InlineData(65536, 5, 3)]
    [InlineData(1883, 0, 3)]
    [InlineData(1883, 0.5, 3)]
    [InlineData(1883, 65536, 3)]
    [InlineData(1883, 5, 0)]
    public void InvalidSettingsAreRejectedBeforeAnyTransport(int port, double keepAlive, double timeout)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MqttConnectionManager(new()
        {
            Port = port, KeepAlive = TimeSpan.FromSeconds(keepAlive), Timeout = TimeSpan.FromSeconds(timeout)
        }));
    }

    private sealed class FakeClient : IBrokerClient
    {
        public bool IsConnected { get; private set; }
        public bool Disposed { get; private set; }
        public bool DropBeforeReturn { get; init; }
        public BrokerConnectionOptions? Options { get; private set; }
        public Func<CancellationToken, Task> Connect { get; init; } = _ => Task.CompletedTask;
        public Func<CancellationToken, Task> Disconnect { get; init; } = _ => Task.CompletedTask;
        public event Action<BrokerDisconnected>? Disconnected;
        public async Task ConnectAsync(BrokerConnectionOptions options, CancellationToken token)
        {
            Options = options;
            await Connect(token);
            IsConnected = true;
            if (DropBeforeReturn) Drop();
        }
        public async Task DisconnectAsync(CancellationToken token)
        {
            await Disconnect(token);
            IsConnected = false;
            Emit(new(true, "NormalDisconnection"));
        }
        public Task PublishCommandAsync(string amrId, string payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Drop() { IsConnected = false; Emit(new(true, "KeepAliveTimeout")); }
        public void Emit(BrokerDisconnected args) => Disconnected?.Invoke(args);
        public void Dispose() { Disposed = true; IsConnected = false; }
    }
}
