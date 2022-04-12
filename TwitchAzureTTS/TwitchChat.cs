namespace TwitchAzureTTS;

using TwitchLib.Client;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;

internal static class TwitchChat
{
    internal static TwitchClient? Client;
    internal static Statuses Status { get; private set; } = Statuses.Disconnected;

    internal static string Channel
    {
        get => Settings.Get("TwitchChannel");
        set => Settings.Set("TwitchChannel", value);
    }

    internal static async Task Connect(bool inBackground = false)
    {
        Disconnect();
        if(!inBackground)
            Renderer.Render();

        if (string.IsNullOrWhiteSpace(Channel))
            return;

        Status = Statuses.Connecting;
        Logger.Info($"<Twitch> Connecting to {Channel} with {(string.IsNullOrWhiteSpace(TwitchTokenKeeper.UserLogin) ? "NULL" : TwitchTokenKeeper.UserLogin)} bot");
        if(!inBackground)
            Renderer.Render();

        if (!(await TwitchTokenKeeper.Validate()))
            if (!(await TwitchTokenKeeper.Refresh()))
            {
                Status = Statuses.Disconnected;
                Logger.Error("<Twitch> Failed to connect to twitch");
                if(!inBackground)
                    Renderer.Render();
                return;
            }


        ConnectionCredentials credentials = new ConnectionCredentials(TwitchTokenKeeper.UserLogin, TwitchTokenKeeper.AccessToken);
        var clientOptions = new ClientOptions
        {
            MessagesAllowedInPeriod = 750,
            ThrottlingPeriod = TimeSpan.FromSeconds(30)
        };
        WebSocketClient customClient = new WebSocketClient(clientOptions);
        Client = new TwitchClient(customClient);
        Client.Initialize(credentials, Channel);

        Client.OnConnected += (_, _) =>
        {
            Status = Statuses.Joining;
            Logger.Info($"<Twitch> Connected, joining {Channel} channel");
            Renderer.Render();
        };
        Client.OnJoinedChannel += (_, ev) =>
        {
            if(ev.Channel.ToLower() == Channel.ToLower())
            {
                Logger.Info($"<Twitch> {Channel} channel joined, listening for messages");
                Status = Statuses.Connected;
            }
            Renderer.Render();
        };
        Client.OnMessageReceived += (_, ev) =>
        {
            AzureTts.AddTextToRead(ev.ChatMessage.Username.ToLower(), ev.ChatMessage.Message);
            Logger.Log(ev.ChatMessage.Username, ev.ChatMessage.Message, preffixFg: EColors.GREEN);
            Renderer.Render();
        };
        Client.OnLeftChannel += (_, ev) =>
        {
            if (ev.Channel.ToLower() == Channel.ToLower())
            {
                Logger.Warning($"<Twitch> {Channel} channel left");
                Status = Statuses.Disconnected;
            }
            Renderer.Render();
        };
        Client.OnDisconnected += (_, _) =>
        {
            if(Status == Statuses.Disconnected)
                Logger.Info($"<Twitch> Disconnected");
            else
                Logger.Warning($"<Twitch> Disconnected");
            Status = Statuses.Disconnected;
            Renderer.Render();
        };

        Client.Connect();
    }

    internal static void Disconnect()
    {
        if (Client is not null)
        {
            Logger.Info($"<Twitch> Disconnecting");
            Client.Disconnect();
            Client = null;
            Status = Statuses.Disconnected;
            Renderer.Render();
        }
    }

    internal enum Statuses
    {
        Connected, Connecting, Joining, Disconnected
    }
}
