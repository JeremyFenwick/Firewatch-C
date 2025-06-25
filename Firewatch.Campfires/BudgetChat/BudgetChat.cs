using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Firewatch.Campfires.GenServers;

namespace Firewatch.Campfires.BudgetChat;

public class BudgetChat : TcpGenServer
{
    private const string WelcomeMessage = "Welcome to budgetchat! What shall I call you?\n";
    private readonly Coordinator _coordinator;
    private abstract record Message;
    private record UserJoined(string UserName, ChannelWriter<IoRequest> Channel) : Message;
    private record UserLeft(string UserName) : Message;
    private record UserMessage(string UserName, string Text) : Message;
    private record IoRequest(string Text) : Message;

    public BudgetChat(int port, IPAddress address) : base(port, address, "Budget chat")
    {
        _coordinator = new Coordinator();
        Task.Run(() => _coordinator.Run(CancellationToken.None));
    }

    private class Coordinator
    {
        private readonly Channel<Message> _channel = Channel.CreateUnbounded<Message>();
        private Dictionary<string, ChannelWriter<IoRequest>> _users = [];

        public async Task PutMessage(Message message)
        {
            await _channel.Writer.WriteAsync(message);
        }

        public async Task Run(CancellationToken token)
        {
            await foreach (var message in _channel.Reader.ReadAllAsync(token))
            {
                if (token.IsCancellationRequested)
                    break;
                switch (message)
                {
                    case UserJoined userJoined:
                        await HandleJoinAsync(userJoined);
                        break;
                    case UserLeft userLeft:
                        await HandleLeaveAsync(userLeft);
                        break;
                    case UserMessage userMessage:
                        await HandleMessageAsync(userMessage);
                        break;
                }
            }
        }
        
        private async Task HandleMessageAsync(UserMessage userMessage)
        {
            Console.WriteLine($"User {userMessage.UserName} says: {userMessage.Text}");
            // Notify all users about the new message
            foreach (var userChannel in _users
                         .Where(kvp => kvp.Key != userMessage.UserName)
                         .Select(kvp => kvp.Value))
            {
                await userChannel.WriteAsync(new IoRequest($"[{userMessage.UserName}] {userMessage.Text}"));
            }
        }

        private async Task HandleLeaveAsync(UserLeft userLeft)
        {
            Console.WriteLine($"User {userLeft.UserName} has left the chat");
            // Remove the user from the list of users
            _users.Remove(userLeft.UserName);
            // Notify all users about the user leaving
            foreach (var userChannel in _users.Values)
            {
                await userChannel.WriteAsync(new IoRequest($"* {userLeft.UserName} has left the room"));
            }
        }

        private async Task HandleJoinAsync(UserJoined message)
        {
            Console.WriteLine($"User {message.UserName} has joined the chat");
            // Notify the new user about the current users in the room
            var notification = new IoRequest($"* The room contains: {string.Join(", ", _users.Keys.ToList())}");
            await message.Channel.WriteAsync(notification);
            // Notify all other users about the new user
            foreach (var userChannel in _users.Values)
            {
                await userChannel.WriteAsync(new IoRequest($"* {message.UserName} has entered the room"));
            }
            // Add the new user to the list of users
            _users[message.UserName] = message.Channel;
        }
    }

    protected override async Task HandlerAsync(TcpClient client)
    {
        await using var stream = client.GetStream();
        var reader = new StreamReader(stream);
        // Get the user's name
        await stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(WelcomeMessage));
        var userName = await reader.ReadLineAsync();
        if (!InvalidUsername(userName))
        {
            // Create a channel for the user and join them to the coordinator
            var userChannel = Channel.CreateUnbounded<IoRequest>();
            var tokenSource = new CancellationTokenSource();
            await _coordinator.PutMessage(new UserJoined(userName!, userChannel.Writer));
            // Listen for incoming messages from the user
            Task.Run(() => ReadUserInputAsync(userName!, reader, tokenSource));
            // Now wait for incoming messages to return to the user
            try
            {
                await foreach (var message in userChannel.Reader.ReadAllAsync(tokenSource.Token))
                {
                    await stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(message.Text + "\n"), tokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // User has left the chat
                await _coordinator.PutMessage(new UserLeft(userName!));
            }
        }
        
        client.Close();
    }

    private async Task ReadUserInputAsync(string user, StreamReader stream, CancellationTokenSource cts)
    {
        while (true)        
        {
            var readLine = await stream.ReadLineAsync();
            if (readLine is null)
                break;
            await _coordinator.PutMessage(new UserMessage(user, readLine));
        }
        // User has left the chat, cancel the token source
        await cts.CancelAsync();
    }

    private static bool InvalidUsername(string? userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
            return true;
        
        return userName
            .Any(c => !char.IsAsciiLetterOrDigit(c));
    }
}

