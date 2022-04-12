using System.Diagnostics;
using System.Text;
using TwitchAzureTTS;

Console.WriteLine("Loading...");
if (!string.IsNullOrWhiteSpace(TwitchChat.Channel))
    await TwitchChat.Connect(true);
AzureMetrics.LogIn();

Renderer.Render();

do
{
    Menu.OnKeyPress(Console.ReadKey(true));
}
while (Menu.IsRunnings);
