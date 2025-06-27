namespace Firewatch.Campfires.GenServers;

public abstract class GenServer
{
    public abstract Task StartAsync();
    public abstract void Stop();
}