using System.Threading.Channels;
using Firewatch.Campfires.SpeedDaemon;

namespace Firewatch.Tests.SpeedDaemon;

public class TicketMasterTests
{
    [Test]
    public async Task TicketMasterTest_WithTimeout()
    {
        var tm = new TicketMaster();
        Task.Run(() => tm.RunAsync());
        var camera1 = new AmCamera(123, 8, 60);
        var camera2 = new AmCamera(123, 9, 60);
        var ticketChannel = Channel.CreateUnbounded<Ticket>();
        var dispatcher = new RegisterDispatcher(1, [123], ticketChannel.Writer);

        await tm.SubmitMessage(camera1);
        await tm.SubmitMessage(camera2);
        await tm.SubmitMessage(dispatcher);
        await tm.SubmitMessage(new Reading(123, 8, "UN1X", 0));
        await tm.SubmitMessage(new Reading(123, 9, "UN1X", 45));
    
        // Wait for the first ticket with a timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var ticket = await ticketChannel.Reader.ReadAsync(cts.Token);
    
        Assert.That(ticket.Plate, Is.EqualTo("UN1X"));
        Assert.That(ticket.Road, Is.EqualTo(123));
        Assert.That(ticket.Speed, Is.GreaterThanOrEqualTo(6000)); // Speed is * 100 in your code
    }
}