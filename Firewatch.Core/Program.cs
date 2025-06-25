using System.Net;
using Firewatch.Campfires.BudgetChat;
using Firewatch.Campfires.GenServers;
using Firewatch.Campfires.MeansToAnEnd;
using Firewatch.Campfires.PrimeTime;
using Firewatch.Campfires.SmokeTest;

namespace Firewatch;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            // Ensure the CAMPFIRE environment variable is set
            var campfire = Environment.GetEnvironmentVariable("CAMPFIRE");
            if (string.IsNullOrEmpty(campfire))
            {
                throw new InvalidOperationException("CAMPFIRE environment variable is not set.");
            }
            // Ensure the CF_PORT environment variable is set
            var portString = Environment.GetEnvironmentVariable("CF_PORT");
            if (string.IsNullOrEmpty(portString))
            {
                throw new InvalidOperationException("CF_PORT environment variable is not set.");
            }
            var portNumber = int.Parse(portString);
            if (portNumber is <= 1000 or > 65535)
            {
                throw new NotSupportedException("CF_PORT must be a valid port number between 1000 and 65535.");
            }
            // Start the campfire server based on the environment variables
            TcpGenServer server = campfire.ToLower() switch
            {
                "smoketest" => new SmokeTest(portNumber, IPAddress.Any) { },
                "primetime" => new PrimeTime(portNumber, IPAddress.Any) { },
                "meanstoanend" => new MeansToAnEnd(portNumber, IPAddress.Any) { },
                "budgetchat" => new BudgetChat(portNumber, IPAddress.Any) { },
                _ => throw new ArgumentOutOfRangeException()
            };
            await server.StartAsync();
        } catch (Exception ex)
        {
            Console.WriteLine($"Error starting campfire: {ex.Message}");
        }
    }
}