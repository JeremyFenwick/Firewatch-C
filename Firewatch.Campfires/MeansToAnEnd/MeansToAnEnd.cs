using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Firewatch.Campfires.GenServers;

namespace Firewatch.Campfires.MeansToAnEnd;

public class MeansToAnEnd(int port, IPAddress address) : TcpGenServer(port, address, "Means to an end")
{
    private abstract record Request;
    private sealed record Insert(int Timestamp, int Price) : Request;
    private sealed record Query(int MinTime, int MaxTime) : Request;

    protected override async Task HandlerAsync(TcpClient client)
    {
        var buffer = new byte[9];
        await using var stream = client.GetStream();
        var dataStore = new List<(int timestamp, int price)>();
        
        while (true)
        {
            await stream.ReadExactlyAsync(buffer);
            try
            {
                var request = ParseRequest(buffer);
                switch (request)
                {
                    case Insert insert:
                        Console.WriteLine($"Inserting >> Timestamp: {insert.Timestamp}, Price: {insert.Price}");
                        dataStore.Add((insert.Timestamp, insert.Price));
                        break;
                    case Query query:
                        Console.WriteLine($"Querying >> MinTime: {query.MinTime}, MaxTime: {query.MaxTime}");
                        var average = RoundedIntervalAverage(dataStore, query.MinTime, query.MaxTime);
                        var payLoad = new byte[4];
                        BinaryPrimitives.WriteInt32BigEndian(payLoad, average);
                        await stream.WriteAsync(payLoad);
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error serving client: {e.Message}");
                break;
            }
        }
        
        client.Close();
    }
    
    private static int RoundedIntervalAverage(List<(int timestamp, int price)> dataStore, int minTime, int maxTime)
    {
        return (int)Math.Round(
            dataStore
            .Where(entry => entry.timestamp >= minTime && entry.timestamp <= maxTime)
            .Select(entry => entry.price)
            .DefaultIfEmpty(0) // Handle case where no data is found
            .Average()
            );
    }

    private static Request ParseRequest(byte[] data)
    {
        var type = (char)data[0];
        var firstNumber = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(1, 4));
        var secondNumber = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(5, 4));
        return type switch
        {
            'I' => new Insert(firstNumber, secondNumber),
            'Q' => new Query(firstNumber, secondNumber),
            _ => throw new InvalidOperationException("Invalid request type")
        };
    }
}