using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Firewatch.Campfires.GenServers;

namespace Firewatch.Campfires.SpeedDaemon;

public class SpeedDaemon : TcpGenServer
{
    private readonly TicketMaster _ticketMaster;

    public SpeedDaemon(int port, IPAddress address) : base(port, address, "Speed Daemon")
    {
        _ticketMaster = new TicketMaster();
        Task.Run(() => _ticketMaster.RunAsync());
    }
    protected override async Task HandlerAsync(TcpClient client)
    {
        var cancellationTokenSource = new CancellationTokenSource();

        try
        {
            var stream = client.GetStream();
            var message = await MessageFactory.ReadMessageAsync(stream);
            switch (message)
            {
                case AmDispatcher dispatcher:
                    Console.WriteLine($"Received dispatcher for roads: {string.Join(", ", dispatcher.Roads)}");
                    var channel = Channel.CreateUnbounded<Ticket>();
                    var registerDispatcher =
                        new RegisterDispatcher(dispatcher.NumRoads, dispatcher.Roads, channel.Writer);
                    await _ticketMaster.SubmitMessage(registerDispatcher);
                    _ = DispatchTicketsAsync(channel.Reader, cancellationTokenSource.Token, stream);
                    await DispatcherLoopAsync(stream, cancellationTokenSource.Token);
                    break;
                case AmCamera camera:
                    Console.WriteLine($"Received camera on road {camera.Road} at mile {camera.Mile}");
                    await CameraLoopAsync(camera, stream, cancellationTokenSource.Token);
                    break;
                default:
                    Console.WriteLine($"Received unsupported message type: {message.GetType()}");
                    throw new NotSupportedException($"Message type {message.GetType()} is not supported.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
        finally
        {
            await cancellationTokenSource.CancelAsync();
            client.Dispose();
        }
    }
    
    private async Task DispatcherLoopAsync(Stream stream, CancellationToken token)
    {
        while (token.IsCancellationRequested == false)
        {
            try
            {
                var message = await MessageFactory.ReadMessageAsync(stream);
                switch (message)
                {
                    case WantHeartBeat wantHeartBeat:
                        Console.WriteLine("Received WantHeartBeat message for dispatcher.");
                        await HeartBeatLoopAsync((int)wantHeartBeat.Interval, stream, token);
                        break;
                    default:
                        throw new NotSupportedException($"Message type {message.GetType()} is not supported.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message: {ex.Message}");
                break; // Exit loop on error
            }
        }
    }
    
    private async Task DispatchTicketsAsync(ChannelReader<Ticket> channel, CancellationToken token, Stream stream)
    {
            await foreach (var ticket in channel.ReadAllAsync(token))
            {
                Console.WriteLine("Dispatching ticket: " + ticket);
                ticket.WriteTo(stream);
            }
    }

    private async Task CameraLoopAsync(AmCamera camera, Stream stream, CancellationToken token)
    {
        while (token.IsCancellationRequested == false)
        {
            try
            {
                var message = await MessageFactory.ReadMessageAsync(stream);
                switch (message)
                {
                    case WantHeartBeat wantHeartBeat:
                        Console.WriteLine($"Received WantHeartBeat message for camera on road {camera.Road} at mile {camera.Mile}.");
                        await HeartBeatLoopAsync((int)wantHeartBeat.Interval, stream, token);
                        break;
                    case Plate plate:
                        Console.WriteLine($"Received plate {plate.PlateNumber} at timestamp {plate.Timestamp} from camera on road {camera.Road} at mile {camera.Mile}.");
                        var reading = new Reading(camera.Road, camera.Mile, plate.PlateNumber, plate.Timestamp);
                        await _ticketMaster.SubmitMessage(reading);
                        break;
                    default:
                        Console.WriteLine($"Received unsupported message type: {message.GetType()} from camera on road {camera.Road} at mile {camera.Mile}.");
                        throw new NotSupportedException($"Message type {message.GetType()} is not supported.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message: {ex.Message}");
                break; // Exit loop on error
            }
        }
    }
    
    private static async Task HeartBeatLoopAsync(int delay, Stream client, CancellationToken cancellationToken)
    {
        var message = new HeartBeat();
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(delay * 1000, cancellationToken);
            message.WriteTo(client);
        }
        Console.WriteLine("HeartBeat loop cancelled.");
    }
}