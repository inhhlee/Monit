using System.Net;
using System.Net.Sockets;

namespace Amr.Verification;

/// <summary>Test-only relay: keep TCP connected while discarding broker responses.</summary>
public sealed class ResponseDropProxy : IAsyncDisposable
{
    readonly TcpListener listener = new(IPAddress.Loopback, 0);
    readonly CancellationTokenSource stop = new();
    readonly Task worker;
    long droppedBytes;
    public volatile bool Drop;
    public long DroppedBytes => Interlocked.Read(ref droppedBytes);
    public Exception? Error { get; private set; }
    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

    public ResponseDropProxy(int targetPort)
    {
        listener.Start(1);
        worker = Relay(targetPort);
    }

    async Task Relay(int targetPort)
    {
        try
        {
            using var client = await listener.AcceptTcpClientAsync(stop.Token);
            using var server = new TcpClient();
            await server.ConnectAsync(IPAddress.Loopback, targetPort, stop.Token);
            using var relay = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
            var upstream = Copy(client.GetStream(), server.GetStream(), false, relay.Token);
            var downstream = Copy(server.GetStream(), client.GetStream(), true, relay.Token);
            await Task.WhenAny(upstream, downstream);
            await relay.CancelAsync();
            try { await Task.WhenAll(upstream, downstream); }
            catch (OperationCanceledException) when (relay.IsCancellationRequested) { }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception error) { if (!stop.IsCancellationRequested) Error = error; }
    }

    async Task Copy(NetworkStream source, NetworkStream destination, bool response, CancellationToken token)
    {
        var buffer = new byte[65536];
        while (true)
        {
            var count = await source.ReadAsync(buffer, token);
            if (count == 0) return;
            if (response && Drop) Interlocked.Add(ref droppedBytes, count);
            else await destination.WriteAsync(buffer.AsMemory(0, count), token);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await stop.CancelAsync();
        listener.Stop();
        await worker;
        stop.Dispose();
    }
}
