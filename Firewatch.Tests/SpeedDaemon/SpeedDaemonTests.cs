using System.Net;
using System.Net.Sockets;
using Firewatch.Campfires.SpeedDaemon;

namespace Firewatch.Tests.SpeedDaemon;

public class SpeedDaemonTests
{
    [Test]
    public void TestSpeedDaemonInitialization()
    {
        // Arrange
        int port = 12345;
        var address = IPAddress.Loopback;

        // Act
        var speedDaemon = new Campfires.SpeedDaemon.SpeedDaemon(port, address);

        // Assert
        Assert.That(speedDaemon, Is.Not.Null);
    }

    [Test]
    public async Task TestSpeedDaemon()
    {
        // Arrange
        var port = 0;
        var address = IPAddress.Loopback;

        // Act
        var speedDaemon = new Campfires.SpeedDaemon.SpeedDaemon(port, address);
        _ = speedDaemon.StartAsync();
        await Task.Delay(500);
        var endPoint = speedDaemon.EndPoint;
        
        // Connect with camera one
        var cameraOne = new AmCamera(123, 8, 60);
        var plateOne = new Plate("UN1X", 0);
        var client = new TcpClient();
        await client.ConnectAsync(address, endPoint.Port);
        var stream = client.GetStream();

        cameraOne.WriteTo(stream);
        plateOne.WriteTo(stream);
        
        // Connect with camera two
        var cameraTwo = new AmCamera(123, 9, 60);
        var plateTwo = new Plate("UN1X", 45);
        var clientTwo = new TcpClient();
        await clientTwo.ConnectAsync(address, endPoint.Port);
        var streamTwo = clientTwo.GetStream();
        cameraTwo.WriteTo(streamTwo);
        plateTwo.WriteTo(streamTwo);
        
        // Connect with dispatcher
        var dispatcherOne = new AmDispatcher(1, [123]);
        var clientThree = new TcpClient();
        await clientThree.ConnectAsync(address, endPoint.Port);
        var streamThree = clientThree.GetStream();
        dispatcherOne.WriteTo(streamThree);
        
        // Collect the ticket from the dispatcher
        var ticket = await MessageFactory.ReadMessageAsync(streamThree);
        Assert.That(ticket, Is.InstanceOf<Ticket>());
    }
}