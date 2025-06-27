using System.Net;
using System.Net.Sockets;

namespace Firewatch.Campfires.GenServers;

public abstract class UdpGenServer : GenServer
{
    private string _serverName;
    private UdpClient _listener;
    private bool _isRunning;

    protected UdpGenServer(int port, string serverName)
    {
        var ipEndPoint = GetEndPoint(port);
        _serverName = serverName;
        _listener = new UdpClient(ipEndPoint);
    }
    
    public override async Task StartAsync()
    {
        Console.WriteLine($"Starting server {_serverName} on port {_listener.Client.LocalEndPoint}...");
        _isRunning = true;
        while (_isRunning)
        {
            var result = await _listener.ReceiveAsync();
            try
            {
                _ = Task.Run(() => HandlerAsync(result));
            }
            catch (Exception _)
            {
                break;
            }
        }
    }

    protected async Task SendAsync(byte[] data, IPEndPoint endPoint)
    {
        if (_isRunning)
        {
            await _listener.SendAsync(data, data.Length, endPoint);
            Console.WriteLine($"Sent data of length {data.Length} to {endPoint}");
        }
    }
    
    public override void Stop()
    {
        _isRunning = false;
        _listener.Close();
        Console.WriteLine($"Server {_serverName} stopped.");
    }
    
    protected abstract Task HandlerAsync(UdpReceiveResult result);

    private IPEndPoint GetEndPoint(int port)
    {
        IPAddress address;
        if (Environment.GetEnvironmentVariable("FLY_APP_NAME") != null)
        {
            var addresses = Dns.GetHostAddresses("fly-global-services");
            if (addresses.Length == 0)
                throw new Exception($"Could not resolve fly global services.");
            address = addresses[0];
        }
        else
        {
            address = IPAddress.Parse("0.0.0.0");
        }
        return new IPEndPoint(address, port);
    }
}