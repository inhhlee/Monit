using System.Net;
using System.Net.Sockets;
using Amr.Verification;
namespace Amr.Verification.Tests;
public sealed class ProxyTests
{
    [Fact]
    public async Task DropModePreservesRequestsAndDiscardsResponses()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var token = timeout.Token;
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        await using var proxy = new ResponseDropProxy(((IPEndPoint)listener.LocalEndpoint).Port);
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, proxy.Port, token);
        using var server = await listener.AcceptTcpClientAsync(token);
        var buffer = new byte[1];
        await server.GetStream().WriteAsync(new byte[] { 1 }, token);
        await client.GetStream().ReadExactlyAsync(buffer, token);
        Assert.Equal(1, buffer[0]);
        proxy.Drop = true;
        await client.GetStream().WriteAsync(new byte[] { 2 }, token);
        await server.GetStream().ReadExactlyAsync(buffer, token);
        Assert.Equal(2, buffer[0]);
        await server.GetStream().WriteAsync(new byte[] { 3 }, token);
        while (proxy.DroppedBytes == 0) await Task.Delay(10, token);
        Assert.False(client.GetStream().DataAvailable);
        Assert.Null(proxy.Error);
        proxy.Drop = false;
        await server.GetStream().WriteAsync(new byte[] { 4 }, token);
        await client.GetStream().ReadExactlyAsync(buffer, token);
        Assert.Equal(4, buffer[0]);
    }
}
