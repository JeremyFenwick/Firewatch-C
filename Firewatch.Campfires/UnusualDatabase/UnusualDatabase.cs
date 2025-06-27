using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Firewatch.Campfires.GenServers;

namespace Firewatch.Campfires.UnusualDatabase;

public class UnusualDatabase : UdpGenServer
{
    private ConcurrentDictionary<string, string> _dataStore;

    private abstract record Command;
    private sealed record GetCommand(string Key) : Command;
    private sealed record SetCommand(string Key, string Value) : Command;
    
    public UnusualDatabase(int port) : base(port, "Unusual Database")
    {
        _dataStore = new ConcurrentDictionary<string, string>();
        _dataStore.AddOrUpdate("version", "unusual database version 1.0. Be careful :)", (k, _) => "1.0");
    }
    
    protected override async Task HandlerAsync(UdpReceiveResult result)
    {
        // Handle the received data here
        Console.WriteLine($"Received data: {BitConverter.ToString(result.Buffer)} from {result.RemoteEndPoint}");
        Command command = ParseCommand(result.Buffer);
        switch (command)
        {
            case SetCommand setCommand:
                Insert(setCommand.Key, setCommand.Value);
                break;
            case GetCommand getCommand:
                await Request(getCommand.Key, result.RemoteEndPoint);
                break;
        }
    }
    
    private void Insert(string key, string value)
    {
        if (key == "version")
            return;
        _dataStore.AddOrUpdate(key, value, (k, _) => value);
        Console.WriteLine($"Inserted: {key} = {value}");
    }

    private async Task Request(string key, IPEndPoint endpoint)
    {
        var value = _dataStore.GetValueOrDefault(key, "");
        var response = Encoding.UTF8.GetBytes($"{key}={value}");
        await SendAsync(response, endpoint);
    }

    private Command ParseCommand(byte[] data)   
    {
        var index = Array.IndexOf(data, (byte)'=');
        if (index == -1)
        {
            return new GetCommand(Encoding.UTF8.GetString(data));
        }
        var key = Encoding.UTF8.GetString(data, 0, index);
        var value = Encoding.UTF8.GetString(data, index + 1, data.Length - index - 1);
        return new SetCommand(key, value);
    }
}