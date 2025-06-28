using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Firewatch.Campfires.GenServers;

namespace Firewatch.Campfires.MobInTheMiddle;

public class MobInTheMiddle(int port, IPAddress address) : TcpGenServer(port, address, "Mob in the Middle")
{
    private const string TonyAddress = "7YWHMfk9JZe0LM0g1ZauHuiSxhI";
    private const string UpstreamServer = "chat.protohackers.com";
    private const int UpstreamPort = 16963;
    private static readonly Regex BoguscoinRegex = new(@"(?<=^|\s)7[a-zA-Z0-9]{25,34}(?=\s|$)", RegexOptions.Compiled);

    
    protected override async Task HandlerAsync(TcpClient client)
    {
        // Setup the client
        var stream = client.GetStream();
        var clientWriter = new StreamWriter(stream) { AutoFlush = true };
        var clientReader = new StreamReader(stream);
        var token = new CancellationTokenSource();
        // Set the upstream connection
        var upstreamClient = new TcpClient(UpstreamServer, UpstreamPort);
        var upstreamStream = upstreamClient.GetStream();
        var upstreamReader = new StreamReader(upstreamStream);
        var upstreamWriter = new StreamWriter(upstreamStream) { AutoFlush = true };
        // Create the two way communication connections
        Task.Run(() => SourceToDest(UpstreamServer, upstreamReader, clientWriter, token));
        await SourceToDest(client.Client.RemoteEndPoint.ToString(), clientReader, upstreamWriter, token);
        // Close the connection down
        Console.WriteLine("Client disconnected, closing connections.");
        client.Close();
        upstreamClient.Close();
    }

    private async Task SourceToDest(string sourceName, StreamReader source, StreamWriter dest, CancellationTokenSource token)
    {
        var buffer = new char[1];
        var messageBuffer = new StringBuilder();
    
        while (!token.IsCancellationRequested)
        {
            var bytesRead = await source.ReadAsync(buffer, 0, buffer.Length);
            if (bytesRead == 0) // End of stream
                break;
            
            messageBuffer.Append(buffer[0]);

            if (buffer[0] != '\n') continue; // Incomplete message
            var message = messageBuffer.ToString();
            Console.WriteLine($"Received message from {sourceName}: {message}");
            
            string processedMessage = BoguscoinRegex.Replace(message, TonyAddress);
            await dest.WriteAsync(processedMessage);
            await dest.FlushAsync();
            
            messageBuffer.Clear();
        }
        await token.CancelAsync();
    }
}