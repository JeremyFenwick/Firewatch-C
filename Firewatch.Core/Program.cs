using System.Net;
using Firewatch.Campfires.BudgetChat;
using Firewatch.Campfires.GenServers;
using Firewatch.Campfires.MeansToAnEnd;
using Firewatch.Campfires.MobInTheMiddle;
using Firewatch.Campfires.PrimeTime;
using Firewatch.Campfires.SmokeTest;
using Firewatch.Campfires.UnusualDatabase;

namespace Firewatch;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            var campfire = Environment.GetEnvironmentVariable("CAMPFIRE");
            var tcpPort = int.Parse(Environment.GetEnvironmentVariable("CF_PORT"));
            var udpPort = int.Parse(Environment.GetEnvironmentVariable("CF_UDP_PORT"));
            
            // Set the campfire server based on the environment variables
            GenServer server = campfire.ToLower() switch
            {
                "smoketest" => new SmokeTest(tcpPort, IPAddress.Any) { },
                "primetime" => new PrimeTime(tcpPort, IPAddress.Any) { },
                "meanstoanend" => new MeansToAnEnd(tcpPort, IPAddress.Any) { },
                "budgetchat" => new BudgetChat(tcpPort, IPAddress.Any) { },
                "unusualdatabase" => new UnusualDatabase(udpPort) { },
                "mobinthemiddle" => new MobInTheMiddle(tcpPort, IPAddress.Any) { },
                _ => throw new ArgumentOutOfRangeException()
            };
            // Start the server
            await server.StartAsync();
        } catch (Exception ex)
        {
            Console.WriteLine($"Error starting campfire: {ex.Message}");
        }
    }
}