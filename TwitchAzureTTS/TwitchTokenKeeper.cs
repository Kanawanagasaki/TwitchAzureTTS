using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Web;

namespace TwitchAzureTTS;

internal static class TwitchTokenKeeper
{
    internal const string TWITCH_CLIENT_ID = "ng916ih3nhx0x3ft9k0dgkplmgo3z8";
    internal const string TOKEN_HANDLER_URL = "https://kanawanagasaki.com/AzureTTS";
    internal const string TWITCH_SCOPES = "channel:moderate chat:edit chat:read whispers:read whispers:edit";

    internal static string AccessToken
    {
        get => Settings.Get("TwitchAccessToken");
        set => Settings.Set("TwitchAccessToken", value);
    }
    internal static string RefreshToken
    {
        get => Settings.Get("TwitchRefreshToken");
        set => Settings.Set("TwitchRefreshToken", value);
    }
    internal static string UserId
    {
        get => Settings.Get("TwitchUserId");
        set => Settings.Set("TwitchUserId", value);
    }
    internal static string UserLogin
    {
        get => Settings.Get("TwitchUserLogin");
        set => Settings.Set("TwitchUserLogin", value);
    }

    internal static bool IsLoggined { get; private set; }

    internal static bool IsRunning;
    private static HttpListener _listener;

    static TwitchTokenKeeper()
    {
        IsRunning = true;
        _listener = new();
        Task.Run(async () =>
        {
            _listener.Prefixes.Add("http://localhost:5888/");
            _listener.Start();
            while(IsRunning)
            {
                var context = await _listener.GetContextAsync();
                var query = HttpUtility.ParseQueryString(context.Request.Url?.Query ?? "");
                Logger.Info("<Twitch> Updating access and refresh tokens");
                if (query.AllKeys?.Any(k => k == "code") ?? false)
                {
                    var code = query["code"] ?? "";
                    using var http = new HttpClient();
                    var response = await http.GetAsync($"{TOKEN_HANDLER_URL}/?code={code}&action=get");
                    var content = await response.Content.ReadAsStringAsync();
                    if(response.StatusCode == HttpStatusCode.OK)
                    {
                        var json = JsonConvert.DeserializeObject<JObject>(content);
                        AccessToken = json?.Value<string>("access_token") ?? "";
                        RefreshToken = json?.Value<string>("refresh_token") ?? "";
                        context.Response.StatusCode = 200;
                        context.Response.OutputStream.Write(Encoding.UTF8.GetBytes("<html><body>Tokens saved, you can close this tab now</html></body>"));

                        Logger.Info("<Twitch> Access and refresh tokens successfully updated");

                        await Validate();
                    }
                    else
                    {
                        context.Response.StatusCode = (int)response.StatusCode;
                        context.Response.OutputStream.Write(Encoding.UTF8.GetBytes("<html><body>"+content+"</html></body>"));

                        Logger.Error($"<Twitch> {(int)response.StatusCode} {response.StatusCode}: {content}");
                    }
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.OutputStream.Write(Encoding.UTF8.GetBytes("<html><body>Bad request</html></body>"));

                    Logger.Error($"<Twitch> 400 Bad Request");
                }
                context.Response.OutputStream.Flush();
                context.Response.OutputStream.Close();
                Renderer.Render();

                await Task.Delay(1000);
            }
        });
    }

    internal static async Task<bool> Validate(bool inBackground = false)
    {
        using HttpClient http = new();
        http.DefaultRequestHeaders.Add("Authorization", "Bearer " + AccessToken);
        var response = await http.GetAsync("https://id.twitch.tv/oauth2/validate");
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();
            var obj = JsonConvert.DeserializeObject<JObject>(json);

            UserId = obj?.Value<string>("user_id") ?? "";
            UserLogin = obj?.Value<string>("login") ?? "";

            IsLoggined = true;
            Logger.Info("<Twitch> Access token is valid");
            if (!inBackground)
                Renderer.Render();
            return true;
        }

        IsLoggined = false;
        Logger.Warning("<Twitch> Failed to validate token");
        if(!inBackground)
            Renderer.Render();
        return false;
    }

    internal static async Task<bool> Refresh(bool inBackground = false)
    {
        using HttpClient http = new();
        var response = await http.GetAsync($"{TOKEN_HANDLER_URL}/?action=refresh&refresh_token={RefreshToken}");
        if(response.StatusCode == HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();
            var obj = JsonConvert.DeserializeObject<JObject>(json);
            AccessToken = obj?.Value<string>("access_token") ?? "";
            RefreshToken = obj?.Value<string>("refresh_token") ?? "";
            Logger.Info("<Twitch> Access token refreshed");
            return await Validate();
        }
        else
        {
            AccessToken = "";
            RefreshToken = "";
            UserId = "";
            UserLogin = "";

            IsLoggined = false;
            Logger.Warning("<Twitch> Failed to refresh token");
            if(!inBackground)
                Renderer.Render();
            return false;
        }
    }

    internal static void Login()
    {
        var url = $"https://id.twitch.tv/oauth2/authorize" +
            $"?response_type=code" +
            $"&client_id={TWITCH_CLIENT_ID}" +
            $"&redirect_uri={HttpUtility.UrlEncode(TOKEN_HANDLER_URL)}" +
            $"&scope={HttpUtility.UrlEncode(TWITCH_SCOPES)}";

        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        Renderer.Render();
    }

    internal static void LogOut()
    {
        AccessToken = "";
        RefreshToken = "";
        UserId = "";
        UserLogin = "";
        IsLoggined = false;
        Logger.Warning("<Twitch> logout");
        Renderer.Render();
    }
}
