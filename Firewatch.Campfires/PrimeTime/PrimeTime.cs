using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices; using System.Text.Json;
using System.Text.Json.Nodes;
using Firewatch.Campfires.GenServers;

namespace Firewatch.Campfires.PrimeTime;

public class PrimeTime(int port, IPAddress address) : TcpGenServer(port, address, "Primetime")
{
    private record Response(string method, bool prime);
    
    protected override async Task HandlerAsync(TcpClient client)
    {
        // Setup the stream and reader/writer
        await using var stream = client.GetStream();
        var writer = new StreamWriter(stream);
        var reader = new StreamReader(stream);
        writer.AutoFlush = true;
        // Read the request
        while (true)
        {
            Response response;
            var readLine = await reader.ReadLineAsync();
            if (readLine is null) return;
            Console.WriteLine($"Received request: {readLine}");
            // If the request is malformed, send a response and return
            try
            {
                var (method, number) = ParseRequest(readLine);
                // Case where number is a float
                if (method != "isPrime")
                    throw new ArgumentException("Invalid method");
                if (number % 1 != 0)
                    response = new Response("isPrime", false);
                // Case where number is an integer
                else
                {
                    var isPrime = IsPrime((int)number);
                    response = new Response("isPrime", isPrime);
                }
            }
            catch (Exception _)
            {
                response = new Response("Malformed request", false);
            }
            // Serialize the response
            var jsonResponse = JsonSerializer.Serialize(response);
            await writer.WriteLineAsync(jsonResponse);
            Console.WriteLine($"Sent response: {jsonResponse}");
            if (response.method != "Malformed request") continue;
            Console.WriteLine("Malformed request received, closing connection.");
            break; // Exit the loop on malformed request
        }

        client.Close();
    }
    
    private static (string, double) ParseRequest(string request)
    {
        var rawNode = JsonNode.Parse(request) ?? throw new ArgumentException("Request deserialization failed");
    
        var method = rawNode["method"]?.ToString();
        if (method is not "isPrime")
            throw new ArgumentException("Invalid method");
    
        var number = rawNode["number"]?.AsValue();
        if (number?.GetValueKind() is not JsonValueKind.Number)
            throw new ArgumentException("Invalid number type");
        
        var doubleValue = number.GetValue<double>();
        return (method, doubleValue);
    }

    private static bool IsPrime(int number)
    {
        
        if (number <= 1) return false;
        if (number <= 3) return true;
        if (number % 2 == 0 || number % 3 == 0) return false;
        
        for (var i = 5; i * i <= number; i += 6)
        {
            if (number % i == 0 || number % (i + 2) == 0)
            {
                return false;
            }
        }
        
        return true;
    }
}