namespace Firewatch.Tests.SpeedDaemon;
using Firewatch.Campfires.SpeedDaemon;

public class MessageTests
{
    [Test]
    public async Task TestDecodeDispatcher()
    {
        var rawData = new ushort[] { 66 };
        var newDispatcher = new AmDispatcher(1, rawData);

        using var ms = new MemoryStream();
        newDispatcher.WriteTo(ms);

        ms.Position = 0;

        var message = await MessageFactory.ReadMessageAsync(ms);

        Assert.That(message, Is.InstanceOf<AmDispatcher>());
        var dispatcherMessage = (AmDispatcher)message;
        Assert.That(dispatcherMessage.NumRoads, Is.EqualTo(1));
        Assert.That(dispatcherMessage.Roads[0], Is.EqualTo(66));
    }
}