using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using TwitchAzureTTS;

int STD_OUTPUT_HANDLE = -11;
uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;

[DllImport("kernel32.dll")]
static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

[DllImport("kernel32.dll")]
static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

[DllImport("kernel32.dll", SetLastError = true)]
static extern IntPtr GetStdHandle(int nStdHandle);

[DllImport("kernel32.dll")]
static extern uint GetLastError();

var iStdOut = GetStdHandle(STD_OUTPUT_HANDLE);
if (!GetConsoleMode(iStdOut, out uint outConsoleMode))
{
    Console.WriteLine("Failed to get output console mode");
    Console.WriteLine("Please use PowerShell or Terminal");
    Console.ReadKey();
    return;
}

outConsoleMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN;
if (!SetConsoleMode(iStdOut, outConsoleMode))
{
    Console.WriteLine($"Failed to set output console mode, error code: {GetLastError()}");
    Console.WriteLine("Please use PowerShell or Terminal");
    Console.ReadKey();
    return;
}

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
