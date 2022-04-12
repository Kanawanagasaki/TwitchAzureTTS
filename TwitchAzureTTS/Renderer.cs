using System.Text;
using System.Text.RegularExpressions;

namespace TwitchAzureTTS;

internal static class Renderer
{
    public static bool IsRunning { get; private set; }

    private static char[,] _buffer;
    private static Color[,] Background;
    private static Color[,] Foreground;

    private static bool _shouldRerender = false;

    static Renderer()
    {
        IsRunning = true;
        _buffer = new char[Console.WindowWidth, Console.WindowHeight];
        Background = new Color[Console.WindowWidth, Console.WindowHeight];
        Foreground = new Color[Console.WindowWidth, Console.WindowHeight];
        Clear();

        Task.Run(() =>
        {
            while (IsRunning)
            {
                if (_buffer.GetLength(0) != Console.WindowWidth || _buffer.GetLength(1) != Console.WindowHeight)
                {
                    lock (_buffer) lock (Background) lock (Foreground)
                            {
                                _buffer = new char[Console.WindowWidth, Console.WindowHeight];
                                Background = new Color[Console.WindowWidth, Console.WindowHeight];
                                Foreground = new Color[Console.WindowWidth, Console.WindowHeight];
                            }

                    Render();
                }

                if (_shouldRerender)
                    Render();

                Task.Delay(100);
            }
        });
    }

    internal static void Write(string text, int x, int y, EColors foreground = EColors.WHITE, EColors background = EColors.BLACK)
    {
        text = text.Replace("\r", "");
        if (text.Contains('\n'))
        {
            var split = text.Split('\n');
            for (int i = 0; i < split.Length; i++)
                Write(split[i], x, y + i, foreground, background);
            return;
        }

        if (y < 0 || y >= _buffer.GetLength(1)) return;
        for (int i = x; i < x + text.Length; i++)
            if (i >= 0 && i < _buffer.GetLength(0))
            {
                _buffer[i, y] = text[i - x];
                Foreground[i, y] = ColorSheet.FromEnum(foreground);
                Background[i, y] = ColorSheet.FromEnum(background);
            }
    }

    internal static void Write(string text, int x, int y, int width, EColors foreground = EColors.WHITE, EColors background = EColors.BLACK)
    {
        text = text.Replace("\r", "");
        if (text.Contains('\n'))
        {
            var split = text.Split('\n');
            for (int i = 0; i < split.Length; i++)
                Write(split[i], x, y + i, width, foreground, background);
            return;
        }

        if (y < 0 || y >= _buffer.GetLength(1)) return;
        for (int i = x; i < x + Math.Min(text.Length, width); i++)
            if (i >= 0 && i < _buffer.GetLength(0))
            {
                _buffer[i, y] = text[i - x];
                Foreground[i, y] = ColorSheet.FromEnum(foreground);
                Background[i, y] = ColorSheet.FromEnum(background);
            }
    }

    internal static void WriteRepeat(string text, int x, int y, int size, bool vertical = false)
    {
        if (vertical)
        {
            if (x < 0 || x >= _buffer.GetLength(0)) return;
            for (int i = y; i < y + size; i++)
                if (i >= 0 && i < _buffer.GetLength(1))
                    _buffer[x, i] = text[(i - y) % text.Length];
        }
        else
        {
            if (y < 0 || y >= _buffer.GetLength(1)) return;
            for (int i = x; i < x + size; i++)
                if (i >= 0 && i < _buffer.GetLength(0))
                    _buffer[i, y] = text[(i - x) % text.Length];
        }
    }

    internal static void WriteRepeat(string text, int x, int y, int size, EColors foreground, EColors background, bool vertical = false)
    {
        if (vertical)
        {
            if (x < 0 || x >= _buffer.GetLength(0)) return;
            for (int i = y; i < y + size; i++)
                if (i >= 0 && i < _buffer.GetLength(1))
                {
                    _buffer[x, i] = text[(i - y) % text.Length];
                    Foreground[x, i] = ColorSheet.FromEnum(foreground);
                    Background[x, i] = ColorSheet.FromEnum(background);
                }
        }
        else
        {
            if (y < 0 || y >= _buffer.GetLength(1)) return;
            for (int i = x; i < x + size; i++)
                if (i >= 0 && i < _buffer.GetLength(0))
                {
                    _buffer[i, y] = text[(i - x) % text.Length];
                    Foreground[i, y] = ColorSheet.FromEnum(foreground);
                    Background[i, y] = ColorSheet.FromEnum(background);
                }
        }
    }

    internal static void Clear(EColors foreground = EColors.WHITE, EColors background = EColors.BLACK)
    {
        for (int ix = 0; ix < _buffer.GetLength(0); ix++)
        {
            for (int iy = 0; iy < _buffer.GetLength(1); iy++)
            {
                _buffer[ix, iy] = ' ';
                Foreground[ix, iy] = ColorSheet.FromEnum(foreground);
                Background[ix, iy] = ColorSheet.FromEnum(background);
            }
        }
    }

    internal static void ClearRect(int x, int y, int width, int height, EColors foreground = EColors.WHITE, EColors background = EColors.BLACK)
    {
        for (int ix = Math.Max(x, 0); ix < _buffer.GetLength(0) && ix < x + width; ix++)
        {
            for (int iy = Math.Max(y, 0); iy < _buffer.GetLength(1) && iy < y + height; iy++)
            {
                _buffer[ix, iy] = ' ';
                Foreground[ix, iy] = ColorSheet.FromEnum(foreground);
                Background[ix, iy] = ColorSheet.FromEnum(background);
            }
        }
    }

    internal static void Render()
    {
        try
        {
            Clear();

            RenderLayout();
            RenderStatus();
            RenderLogs();
            RenderMenu();
            RenderTextInput();
            RenderRangeInput();
            RenderSelect();

            Flush();

            _shouldRerender = false;
        }
        catch
        {
            _shouldRerender = true;
        }
    }

    private static void RenderLayout()
    {
        int elHeight = Menu.MaxRows + 3;
        WriteRepeat("_", 0, Console.WindowHeight - elHeight - 3, Console.WindowWidth);

        string text = "Twitch Azure TTS - made by Kanawanagasaki | Arrows - Navigate | Spacebar - Select | Esc - Back |";

        Write(text, 0, Console.WindowHeight - 1, EColors.BLACK, EColors.WHITE);
        WriteRepeat(" ", text.Length, Console.WindowHeight - 1, Console.WindowWidth - text.Length, EColors.BLACK, EColors.WHITE);

        int center = Console.WindowWidth / 2;
        WriteRepeat("|", center, 0, Console.WindowHeight - elHeight - 2, true);

        int width = Console.WindowWidth / 2 - 2;
        Write("Status", 2, 1, width);
        Write("Log", center + 3, 1, width);
    }

    private static void RenderStatus()
    {
        int x = 3;
        int y = 3;
        int width = Console.WindowWidth / 2 - 5;
        int height = Console.WindowHeight - Menu.MaxRows - 9;

        List<string> lines = new();

        lines.Add("Twitch");
        lines.Add($"  Is authenicated: {TwitchTokenKeeper.IsLoggined}");
        if (TwitchTokenKeeper.IsLoggined)
            lines.Add($"  Authenticated with: {TwitchTokenKeeper.UserLogin}");
        lines.Add($"  Channel to connect: {TwitchChat.Channel}");
        lines.Add($"  Status: {TwitchChat.Status}");
        lines.Add($"  Ignore commands (messages start with !): {TwitchChat.IgnoreCommands}");

        lines.Add("");

        lines.Add("AzureTTS");
        lines.Add("  Is key entered: " + (!string.IsNullOrWhiteSpace(AzureTts.Key)));
        lines.Add("  Region: " + AzureTts.Region);
        if (!string.IsNullOrWhiteSpace(AzureTts.DefaultVoice))
            lines.Add("  Default voice: " + AzureTts.DefaultVoice);
        if (!string.IsNullOrWhiteSpace(AzureTts.DefaultVoiceVolume))
            lines.Add("  Default voice volume: " + AzureTts.DefaultVoiceVolume);
        if (!string.IsNullOrWhiteSpace(AzureTts.DefaultVoicePitch) && int.TryParse(AzureTts.DefaultVoicePitch, out int pitch))
            lines.Add($"  Default voice pitch: {(pitch < 0 ? "" : "+")}{pitch}Hz");
        if (!string.IsNullOrWhiteSpace(AzureTts.DefaultVoiceRate))
            lines.Add("  Default voice rate: " + AzureTts.DefaultVoiceRate);

        lines.Add("");

        lines.Add("Azure Metrics");
        lines.Add("  Is authorized: " + AzureMetrics.IsAuthorized);
        lines.Add("  Is tenant id entered: " + (!string.IsNullOrWhiteSpace(AzureMetrics.TenantId)));
        lines.Add("  Is the resource known: " + (!string.IsNullOrWhiteSpace(AzureMetrics.ResourceId)));

        lines.Add("");

        if (AzureMetrics.IsAuthorized && !string.IsNullOrWhiteSpace(AzureMetrics.TenantId) && !string.IsNullOrWhiteSpace(AzureMetrics.ResourceId))
        {
            if (AzureMetrics.Metrics is null)
                lines.Add("Metrics: Loading...");
            else
            {
                double[] values = AzureMetrics.Metrics.Metrics.SelectMany(m => m.TimeSeries).SelectMany(ts => ts.Values).Select(val => val.Total ?? 0).ToArray();
                var usedSum = values?.Sum() ?? 0;
                var max = values?.Max() ?? 0;

                lines.Add("Metrics");
                lines.Add($"  {string.Join("", Regex.Replace(string.Join("", usedSum.ToString().Reverse()), ".{3}", "$0 ").Reverse())}/500 000");

                int graphY = y + lines.Count + 1;

                if (max > 0 && graphY < y + height)
                {
                    Write($"{max} - max", x, graphY, width);
                    int graphHeight = Console.WindowHeight - Menu.MaxRows - 6 - graphY;

                    for (double i = 0; i < width; i++)
                    {
                        int index = (int)Math.Floor((i / (width - 1)) * (values!.Length - 1));
                        int columnHeight = (int)Math.Round((values[index] / max) * graphHeight);
                        WriteRepeat(" ", x + (int)i, graphY + graphHeight - columnHeight, columnHeight, EColors.BLACK, EColors.RED, true);
                    }

                    for (double i = 0; i < width; i++)
                    {
                        double index = (i / (width - 1)) * (values!.Length - 1);
                        double multiplier = index % 1;
                        double val1 = values[(int)index];
                        double val2 = values[(int)index + (multiplier == 0 ? 0 : 1)];
                        double val = val1 + (val2 - val1) * multiplier;

                        int columnHeight = (int)Math.Round((val / max) * graphHeight);
                        WriteRepeat(" ", x + (int)i, graphY + graphHeight - columnHeight, columnHeight, EColors.BLACK, EColors.WHITE, true);
                    }
                }
            }
        }

        for (int i = 0; i < lines.Count && i < height; i++)
        {
            Write(lines[i], x, y + i, width);
        }
    }

    private static void RenderLogs()
    {
        int x = Console.WindowWidth / 2 + 4;
        int y = 3;
        int width = Console.WindowWidth / 2 - 5;
        int height = Console.WindowHeight - Menu.MaxRows - 9;

        if (width <= 0 || height <= 0) return;

        List<(string text, EColors fg, EColors bg, bool last)> words = new();
        foreach (var item in Logger.Items)
        {
            words.Add(($"[{item.Datetime:HH:mm:ss}]", EColors.GREY, EColors.BLACK, false));
            foreach (var word in item.Preffix.Split(" "))
                words.Add((word, item.PreffixFg, item.PreffixBg, false));
            foreach (var word in item.Text.Split(" "))
                words.Add((word, item.TextFg, item.TextBg, false));
            words[words.Count - 1] = (words[words.Count - 1].text, item.TextFg, item.TextBg, true);
        }
        for (int i = 0; i < words.Count; i++)
        {
            if (words[i].text.Length > width)
            {
                var isLast = words[i].last;
                var text = words[i].text;
                words[i] = (text[..width], words[i].fg, words[i].bg, false);
                words.Insert(i + 1, (text[(width)..], words[i].fg, words[i].bg, isLast));
            }
        }

        List<List<(string text, EColors fg, EColors bg, bool last)>> lines = new();
        lines.Add(new());
        while (words.Count > 0)
        {
            int lineWidth = lines.Last().Sum(w => w.text.Length + 1);
            if (lineWidth + words[0].text.Length + 1 > width)
                lines.Add(new());

            if (words[0].text.Contains('\n'))
            {
                var split = words[0].text.Split('\n');
                for (int i = 0; i < split.Length - 1; i++)
                {
                    lines.Last().Add((split[i], words[0].fg, words[0].bg, true));
                    lines.Add(new());
                }
                lines.Last().Add((split.Last(), words[0].fg, words[0].bg, words[0].last));
            }
            else lines.Last().Add(words[0]);

            if (words[0].last)
                lines.Add(new());
            words.RemoveAt(0);
        }

        var finalLines = lines.TakeLast(height).ToArray();
        for (int i = 0; i < finalLines.Length; i++)
        {
            int ix = 0;
            foreach (var word in finalLines[i])
            {
                Write(word.text, x + ix, y + i, word.fg, word.bg);
                ix += word.text.Length + 1;
            }
        }
    }

    private static void RenderMenu()
    {
        int elWidth = (Console.WindowWidth - 1) / Menu.Columns.Length;
        int elHeight = Menu.MaxRows + 4;
        for (int ix = 0; ix < Menu.Columns.Length; ix++)
        {
            int titleX = ix * elWidth + (elWidth / 2 - Menu.Columns[ix].Name.Length / 2);
            Write(Menu.Columns[ix].Name, titleX, Console.WindowHeight - elHeight, EColors.YELLOW);

            var group = Menu.Columns[ix].Items.GroupBy(i => i.Offset);
            int maxOffset = group.Max(g => g.Key);
            int groupWidth = elWidth / (maxOffset + 1);
            foreach (var gr in group)
            {
                int maxWidth = gr.Max(i => i.Name().Length);
                int x = (ix * elWidth + groupWidth * gr.Key) + (groupWidth / 2 - maxWidth / 2);

                int iy = 0;
                foreach (var item in gr)
                {
                    int y = Console.WindowHeight - elHeight + iy + 2;

                    if (ix == Menu.Column && iy == Menu.Row && item.Offset == Menu.Offset)
                        Write("> " + item.Name(), x, y, EColors.CYAN1);
                    else Write(item.Name(), x, y);

                    iy++;
                }
            }
        }
    }

    private static void RenderTextInput()
    {
        if (Menu.Input is null) return;

        int width = Console.WindowWidth / 2;
        int height = 7;
        int x = Console.WindowWidth / 2 - width / 2;
        int y = Console.WindowHeight / 2 - height / 2;

        ClearRect(x, y, width, height);

        WriteRepeat(" ", x, y, width, EColors.BLACK, EColors.MAGENTA3);
        WriteRepeat(" ", x, y + height - 1, width, EColors.BLACK, EColors.MAGENTA3);
        WriteRepeat(" ", x, y, height, EColors.BLACK, EColors.MAGENTA3, true);
        WriteRepeat(" ", x + 1, y, height, EColors.BLACK, EColors.MAGENTA3, true);
        WriteRepeat(" ", x + width - 1, y, height, EColors.BLACK, EColors.MAGENTA3, true);
        WriteRepeat(" ", x + width - 2, y, height, EColors.BLACK, EColors.MAGENTA3, true);

        Write(Menu.Input.Title, x + 3, y + 2);

        int lineLen = 0;

        int textWidth = width - 6;
        var text = Menu.Input.Input;
        if (text.Length < textWidth)
            lineLen = textWidth - text.Length;
        else if (text.Length > textWidth)
            text = text.Substring(text.Length - textWidth, textWidth);

        if (Menu.Input.IsPassword)
            Write(new string(Enumerable.Range(0, text.Length).Select(_ => '*').ToArray()), x + 3, y + 4);
        else Write(text, x + 3, y + 4);

        if (lineLen > 0)
            WriteRepeat("_", x + 3 + text.Length, y + 4, lineLen);
    }

    private static void RenderRangeInput()
    {
        if (Menu.Range is null) return;

        int width = Console.WindowWidth / 2;
        int height = 9;
        int x = Console.WindowWidth / 2 - width / 2;
        int y = Console.WindowHeight / 2 - height / 2;

        ClearRect(x, y, width, height);

        WriteRepeat(" ", x, y, width, EColors.BLACK, EColors.MAGENTA3);
        WriteRepeat(" ", x, y + height - 1, width, EColors.BLACK, EColors.MAGENTA3);
        WriteRepeat(" ", x, y, height, EColors.BLACK, EColors.MAGENTA3, true);
        WriteRepeat(" ", x + 1, y, height, EColors.BLACK, EColors.MAGENTA3, true);
        WriteRepeat(" ", x + width - 1, y, height, EColors.BLACK, EColors.MAGENTA3, true);
        WriteRepeat(" ", x + width - 2, y, height, EColors.BLACK, EColors.MAGENTA3, true);

        Write(Menu.Range.Title, x + 3, y + 2, width - 6);

        Write(Menu.Range.MinValue.ToString("0.##"), x + 3, y + 4, width - 6);
        Write(Menu.Range.MaxValue.ToString("0.##"), x + width - 3 - Menu.Range.MaxValue.ToString("0.##").Length, y + 4, width - 6);
        WriteRepeat("-", x + 3, y + 5, width - 6);

        double percent = Math.Clamp((Menu.Range.Value - Menu.Range.MinValue) / (Menu.Range.MaxValue - Menu.Range.MinValue), 0d, 1d);
        Write("O", x + 3 + (int)((width - 7) * percent), y + 5);

        var value = Menu.Range.Value.ToString("0.##");
        int valueX = Math.Clamp(x + 3 + (int)((width - 6) * percent) - value.Length / 2, x + 3, x + width - 3 - value.Length);
        Write(value, valueX, y + 6);
    }

    private static void RenderSelect()
    {
        if (Menu.Select is null) return;

        int width = Console.WindowWidth / 2;
        int height = Console.WindowHeight / 2;
        int x = Console.WindowWidth / 2 - width / 2;
        int y = Console.WindowHeight / 2 - height / 2;

        ClearRect(x, y, width, height);

        WriteRepeat(" ", x, y, width, EColors.BLACK, EColors.MAGENTA3);
        WriteRepeat(" ", x, y + height - 1, width, EColors.BLACK, EColors.MAGENTA3);
        WriteRepeat(" ", x, y, height, EColors.BLACK, EColors.MAGENTA3, true);
        WriteRepeat(" ", x + 1, y, height, EColors.BLACK, EColors.MAGENTA3, true);
        WriteRepeat(" ", x + width - 1, y, height, EColors.BLACK, EColors.MAGENTA3, true);
        WriteRepeat(" ", x + width - 2, y, height, EColors.BLACK, EColors.MAGENTA3, true);

        int selectedIndex = 0;
        List<(string name, Menu.SelectInput select, int depth, bool selected, bool isItem)> lines = new();
        Action<Menu.SelectInput, int, bool>? recursion = null;
        recursion = (select, depth, selected) =>
        {
            lines.Add((select.Title, select, depth, selected, false));
            if (select.IsExpanded)
            {
                for (int i = 0; i < select.Childs.Length; i++)
                    recursion!(select.Childs[i], depth + 1, select.SelectedIndex == i);
                for (int i = 0; i < select.Items.Length; i++)
                    lines.Add((select.Items[i].Key, select, depth + 1, select.SelectedIndex - select.Childs.Length == i, true));
                selectedIndex += select.SelectedIndex + 1;
            }
        };
        recursion(Menu.Select, 0, true);

        int amount = height - 5;
        var filtered = lines.Skip(Math.Clamp(selectedIndex - amount / 2, 0, Math.Max(0, lines.Count - amount))).Take(amount).ToArray();
        for (int i = 0; i < filtered.Length; i++)
        {
            string text = new string(Enumerable.Range(0, filtered[i].depth * 2).Select(_ => ' ').ToArray()) + (filtered[i].selected ? "> " : "") + filtered[i].name;
            if (filtered[i].selected && (!filtered[i].select.IsExpanded || filtered[i].isItem))
                text = "|" + text.Substring(1);
            Write(text, x + 3, y + 2 + i, width - 6, filtered[i].selected ? EColors.CYAN1 : EColors.WHITE);
        }

        string hint = "Up/Down-Navigate, Right-Expand, Left-Collapse, Enter-Select";
        Write(hint, x + 2, y + height - 2, width - 4, EColors.BLACK, EColors.WHITE);
        WriteRepeat(" ", x + 2 + hint.Length, y + height - 2, width - 4 - hint.Length, EColors.BLACK, EColors.WHITE);
    }

    internal static void Flush()
    {
        var builder = new StringBuilder();

        var buffBg = ColorSheet.BLACK;
        var buffFg = ColorSheet.WHITE;
        for (int iy = 0; iy < _buffer.GetLength(1); iy++)
        {
            for (int ix = 0; ix < _buffer.GetLength(0); ix++)
            {
                if (Background[ix, iy] != buffBg)
                {
                    builder.Append($"\x1b[48;5;{Background[ix, iy].Id}m");
                    buffBg = Background[ix, iy];
                }
                if (Foreground[ix, iy] != buffFg)
                {
                    builder.Append($"\x1b[38;5;{Foreground[ix, iy].Id}m");
                    buffFg = Foreground[ix, iy];
                }
                builder.Append(_buffer[ix, iy] < 32 ? ' ' : _buffer[ix, iy]);
            }
        }
        builder.Append("\x1b[0m");

        Console.SetCursorPosition(0, 0);
        Console.Write(builder.ToString());
        Console.CursorVisible = false;
    }
}
