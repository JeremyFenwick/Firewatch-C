using System.Net;
using System.Net.Sockets;
using Firewatch.Campfires.GenServers;

namespace Firewatch.Campfires.MobInTheMiddle;

public class MobInTheMiddle(int port, IPAddress address) : TcpGenServer(port, address, "Mob in the Middle")
{
    private const string TonyAddress = "7YWHMfk9JZe0LM0g1ZauHuiSxhI";
    private const string UpstreamServer = "chat.protohackers.com";
    private const int UpstreamPort = 16963;
    protected override Task HandlerAsync(TcpClient client)
    {
        throw new NotImplementedException();
    }

    private string ProcessMessage(string message)
    {
        var words = message.Split(' ');
        for (var i = 0; i < words.Length; i++)
        {
            if (IsBogusCoin(words[i]))
            {
                words[i] = TonyAddress;
            }
        }
        // Dummy comment
        return string.Join(' ', words);
    }

    private static bool IsBogusCoin(string address)
    {
        if (address.Length is < 26 or > 35)
            return false;
        return address[0] == '7' && address.All(char.IsLetterOrDigit);
    }
}