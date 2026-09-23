using System.Net;
using System.Net.Sockets;

namespace Amr.Verification;

/// <summary>
/// 테스트 전용 TCP 릴레이 프록시 클래스입니다.
/// Broker와 Client 간의 TCP 연결은 유지한 채, 설정(Drop)에 따라 
/// Broker에서 Client로 전달되는 응답 데이터만 고의로 누락시켜 네트워크 응답 차단 상황을 모사합니다.
/// </summary>
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

    /// <summary>
    /// 소켓 간에 데이터를 복사하며 중계합니다.
    /// response 플래그가 true이고 Drop 플래그가 설정되어 있으면, 수신된 바이트를 목적지에 쓰지 않고 버립니다(누락).
    /// </summary>
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
