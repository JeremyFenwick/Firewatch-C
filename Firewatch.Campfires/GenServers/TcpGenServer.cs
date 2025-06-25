using System.Net;
using System.Net.Sockets;

namespace Firewatch.Campfires.GenServers;

public abstract class TcpGenServer(int port, IPAddress address, string serverName)
{
    private string ServerName { get; } = serverName;
    private bool _isRunning = false;
    private readonly TcpListener _listener = new(address, port);

    public async Task StartAsync()
    {
        _listener.Start();
        _isRunning = true;
        Console.WriteLine($"Server {ServerName} started on {_listener.LocalEndpoint}");

        while (_isRunning)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync();
                Console.WriteLine($"Client connected: {client.Client.RemoteEndPoint}");
                _ = Task.Run(() => HandlerAsync(client));
            }
            catch (Exception _)
            {
                break;
            }
        }
    }

    public void Stop()
    {
        _isRunning = true;
        _listener.Stop();
        Console.WriteLine($"Server {ServerName} stopped.");
    }

    protected abstract Task HandlerAsync(TcpClient client);
}