namespace TwitchAzureTTS;

internal static class Logger
{
    private static List<LogItem> _log = new();
    public static IReadOnlyList<LogItem> Items => _log;

    internal static void Log(string preffix, string text, EColors preffixFg = EColors.WHITE, EColors preffixBg = EColors.BLACK, EColors textFg = EColors.WHITE, EColors textBg = EColors.BLACK)
    {
        if (_log.Count > 1000) _log.RemoveAt(0);
        _log.Add(new(DateTime.Now, preffix + ":", preffixFg, preffixBg, text, textFg, textBg));
    }

    internal static void Info(string text)
        => Log("info", text, EColors.BLACK, EColors.WHITE, EColors.WHITE, EColors.BLACK);

    internal static void Warning(string text)
        => Log("warn", text, EColors.BLACK, EColors.YELLOW, EColors.WHITE, EColors.BLACK);

    internal static void Error(string text)
        => Log("err", text, EColors.BLACK, EColors.RED, EColors.WHITE, EColors.BLACK);

    internal record LogItem(DateTime Datetime, string Preffix, EColors PreffixFg, EColors PreffixBg, string Text, EColors TextFg, EColors TextBg);
}
