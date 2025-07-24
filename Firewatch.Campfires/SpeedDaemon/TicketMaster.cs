using System.Threading.Channels;

namespace Firewatch.Campfires.SpeedDaemon;

public sealed class TicketMaster()
{
    // Internal data structures
    private readonly Channel<IMessage> _ticketMasterChannel = Channel.CreateUnbounded<IMessage>(); // Incoming messages
    private readonly Dictionary<ushort, Road> _roads = new(); // Road id to Road
    private readonly List<Ticket> _pendingTickets = []; // Tickets to be send to a dispatcher
    private readonly Dictionary<string, HashSet<uint>> _carTicketHistory = new(); // Plate to list of timestamps
    
    // Records for internal processing
    private record Measurement(ushort Mile, uint Timestamp);
    private record Road(ushort SpeedLimit, Dictionary<string, List<Measurement>> CarMeasurements, List<ChannelWriter<Ticket>> Dispatchers);
    
    // Public methods
    
    public async Task SubmitMessage(IMessage msg)
    {
        await _ticketMasterChannel.Writer.WriteAsync(msg);
    }

    public async Task RunAsync()
    {
        await foreach(var message in _ticketMasterChannel.Reader.ReadAllAsync()) 
        {
            switch (message)
            {
                case AmCamera camera:
                    Console.WriteLine("Adding camera: " + camera.Road + "at position " + camera.Mile);
                    AddCamera(camera);
                    break;
                case Reading reading:
                    Console.WriteLine("Processing reading: " + reading.PlateNumber + " on road " + reading.Road);
                    await ProcessCameraReading(reading);
                    break;
                case RegisterDispatcher dispatcher:
                    Console.WriteLine("Registering dispatcher that covers roads: " + string.Join(", ", dispatcher.Roads));
                    AddDispatcher(dispatcher);
                    break;
                default:
                    Console.WriteLine("Received unsupported message type: " + message.GetType());
                    throw new NotSupportedException($"Message type {message.GetType()} is not supported.");
            }
        }
    }
    
    // Internal methods

    private async Task ProcessCameraReading(Reading reading)
    {
        var road = _roads[reading.Road];
        if (!road.CarMeasurements.ContainsKey(reading.PlateNumber))
        {
            road.CarMeasurements[reading.PlateNumber] = [];
        }
        var carMeasurements = road.CarMeasurements[reading.PlateNumber];
        carMeasurements.Add(new Measurement(reading.Mile, reading.Timestamp));
        if (carMeasurements.Count >= 2)
            GenerateCarTickets(reading.Road, reading.PlateNumber);
        if (_pendingTickets.Count >= 1)
            await SendTicketsAsync();
    }

    private void AddCamera(AmCamera camera)
    {
        if (!_roads.ContainsKey(camera.Road))
        {
            _roads[camera.Road] = new Road(camera.SpeedLimit, new Dictionary<string, List<Measurement>>(), []);
        }
        else if (_roads[camera.Road].SpeedLimit != camera.SpeedLimit)
        {
            // If the speed limit has changed, update it
            _roads[camera.Road] = _roads[camera.Road] with { SpeedLimit = camera.SpeedLimit };
        }
    }

    private void GenerateCarTickets(ushort road, string plate)
    {
        var measurements = _roads[road].CarMeasurements[plate];
        // Sort the measurements by timestamp
        measurements.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
        // Iterate through measurement pairs to generate tickets
        var usedMeasurements = new List<Measurement>();
        for (var i = 1; i < measurements.Count; i++)
        {
            var firstM = measurements[i - 1];
            var secondM = measurements[i];
            // Calculate the speed between the two measurements
            var speed = (secondM.Mile - firstM.Mile) * 3600 / (secondM.Timestamp - firstM.Timestamp);
            if (speed <= _roads[road].SpeedLimit) continue;
            // Calculate the ticket days
            var dayOne = firstM.Timestamp / 86400;
            var dayTwo = secondM.Timestamp / 86400;
            var ticket = new Ticket(plate, road, firstM.Mile, firstM.Timestamp, secondM.Mile, secondM.Timestamp, (ushort)(speed * 100));
            _pendingTickets.Add(ticket);
            if (dayTwo != dayOne)
            {
                // If the ticket spans multiple days, we need to create a second ticket
                _pendingTickets.Add(ticket);
            }
            // Mark the measurements as used
            usedMeasurements.Add(measurements[i - 1]);
            usedMeasurements.Add(measurements[i]);
        }
        // Remove used measurements from the list
        foreach (var usedMeasurement in usedMeasurements)
        {
            measurements.Remove(usedMeasurement);
        }
    }
    
    private void AddDispatcher(RegisterDispatcher dispatcher)
    {
        foreach (var road in dispatcher.Roads)
        {
            if (!_roads.ContainsKey(road))
                _roads[road] = new Road(0, new Dictionary<string, List<Measurement>>(), []);
            _roads[road].Dispatchers.Add(dispatcher.TicketChannel);
        }
    }

    private async Task SendTicketsAsync()
    {
        var random = new Random();
        var ticketsToProcess = _pendingTickets.ToList(); // Avoid modifying the list during iteration

        foreach (var ticket in ticketsToProcess)
        {
            if (!_roads.TryGetValue(ticket.Road, out var road) || road.Dispatchers.Count == 0)
                continue;

            var ticketDayOne = ticket.TimeStampOne / 86400;
            var ticketDayTwo = ticket.TimeStampTwo / 86400;
            
            _pendingTickets.Remove(ticket);

            // We can only send tickets if they are not already sent for the same plate on the same day
            if (_carTicketHistory.TryGetValue(ticket.Plate, out var value) &&
                (value.Contains(ticketDayOne) || value.Contains(ticketDayTwo)))
                continue;

            var channel = road.Dispatchers[random.Next(road.Dispatchers.Count)];
            await channel.WriteAsync(ticket);
            
            if (!_carTicketHistory.ContainsKey(ticket.Plate))
                _carTicketHistory[ticket.Plate] = [];

            _carTicketHistory[ticket.Plate].Add(ticketDayOne);
            _carTicketHistory[ticket.Plate].Add(ticketDayTwo);
        }
    }
}