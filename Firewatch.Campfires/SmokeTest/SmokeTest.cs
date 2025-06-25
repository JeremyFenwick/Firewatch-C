using System.Net;
using System.Net.Sockets;
using Firewatch.Campfires.GenServers;

namespace Firewatch.Campfires.SmokeTest;

public sealed class SmokeTest(int port, IPAddress address) : TcpGenServer(port, address, "Smoketest")
{
    protected override async Task HandlerAsync(TcpClient client)
    {
        await using var stream = client.GetStream();
        var buffer = new byte[4096];

        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer)) != 0)
        {
            Console.WriteLine($"Received {bytesRead} bytes from {client.Client.RemoteEndPoint}");
            await stream.WriteAsync(buffer.AsMemory(0, bytesRead));
        }
        client.Close();
    }
}