namespace TwitchAzureTTS;

using Microsoft.CognitiveServices.Speech;
using System.Collections.ObjectModel;
using System.Globalization;

internal static class AzureTts
{
    internal static bool IsRunning;

    internal static string Key
    {
        get => Settings.Get("AzureSpeechKey1");
        set => Settings.Set("AzureSpeechKey1", value);
    }
    internal static string Region
    {
        get => Settings.Get("AzureSpeechRegion");
        set => Settings.Set("AzureSpeechRegion", value);
    }
    internal static string DefaultVoice
    {
        get => Settings.Get("AzureSpeechDefaultVoice");
        set => Settings.Set("AzureSpeechDefaultVoice", value);
    }
    internal static string DefaultVoiceVolume
    {
        get => Settings.Get("AzureSpeechDefaultVoiceVolume", "50");
        set => Settings.Set("AzureSpeechDefaultVoiceVolume", value);
    }
    internal static string DefaultVoicePitch
    {
        get => Settings.Get("AzureSpeechDefaultVoicePitch", "0");
        set => Settings.Set("AzureSpeechDefaultVoicePitch", value);
    }
    internal static string DefaultVoiceRate
    {
        get => Settings.Get("AzureSpeechDefaultVoiceRate", "1");
        set => Settings.Set("AzureSpeechDefaultVoiceRate", value);
    }

    private static List<(string username, string text)> _textToRead = new();
    private static SpeechConfig? _conf;
    private static SpeechSynthesizer? _synthesizer;

    internal record UserVoiceInfo(string username, string? voice, int? volume, int? pitch, double? rate);
    internal static Dictionary<string, UserVoiceInfo> UserVoices = new();

    static AzureTts()
    {
        IsRunning = true;

        ParseUserVoices(Settings.GetArr("AzureSpeechUserVoices"));

        try
        {
            _conf = SpeechConfig.FromSubscription(Key, Region);
            _synthesizer = new SpeechSynthesizer(_conf);
        }
        catch { }

        Task.Run(async () =>
        {
            while (IsRunning)
            {
                if (string.IsNullOrWhiteSpace(Key) || string.IsNullOrWhiteSpace(Region))
                    continue;

                if (_conf is null || _synthesizer is null)
                {
                    try
                    {
                        _conf = SpeechConfig.FromSubscription(Key, Region);
                        _synthesizer = new SpeechSynthesizer(_conf);
                    }
                    catch (Exception e)
                    {
                        Logger.Error("<AzureTTS> In Await Loop: " + e.Message);
                        continue;
                    }
                }

                if (Key != _conf.SubscriptionKey || Region != _conf.Region)
                {
                    _synthesizer.Dispose();

                    _conf = SpeechConfig.FromSubscription(Key, Region);
                    _synthesizer = new SpeechSynthesizer(_conf);
                }

                if (_textToRead.Count > 0)
                {
                    int num = 0;
                    double dnum = 0;

                    var voice = DefaultVoice;
                    var volume = int.TryParse(DefaultVoiceVolume, out num) ? num : 50;
                    var pitch = int.TryParse(DefaultVoicePitch, out num) ? num : 0;
                    var rate = (double.TryParse(DefaultVoiceRate.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out dnum) ? dnum : 1).ToString("0.00").Replace(",", ".");

                    if (UserVoices.ContainsKey(_textToRead[0].username ?? ""))
                    {
                        if (UserVoices[_textToRead[0].username ?? ""].voice is not null)
                            voice = UserVoices[_textToRead[0].username ?? ""].voice;
                        if (UserVoices[_textToRead[0].username ?? ""].volume is not null)
                            volume = UserVoices[_textToRead[0].username ?? ""].volume ?? 50;
                        if (UserVoices[_textToRead[0].username ?? ""].pitch is not null)
                            pitch = UserVoices[_textToRead[0].username ?? ""].pitch ?? 0;
                        if (UserVoices[_textToRead[0].username ?? ""].rate is not null)
                            rate = (UserVoices[_textToRead[0].username ?? ""].rate ?? 1d).ToString("0.00").Replace(",", ".");
                    }

                    string ssml = $@"
                    <speak version=""1.0"" xmlns=""http://www.w3.org/2001/10/synthesis"" xml:lang=""en-US"">
                        <voice name=""{voice}"">
                            <prosody volume=""{volume}"" pitch=""{(pitch < 0 ? "" : "+")}{pitch}Hz"" rate=""{rate}"">
                                {_textToRead[0].text}
                            </prosody>
                        </voice >
                    </speak>";

                    try
                    {
                        await _synthesizer.SpeakSsmlAsync(ssml);
                    }
                    catch (Exception e)
                    {
                        Logger.Error("<AzureTTS> " + e.Message);
                        Renderer.Render();
                    }
                    lock (_textToRead)
                        _textToRead.RemoveAt(0);
                }
                else await Task.Delay(1000);
            }
        });
    }

    internal static void ReloadSynthesizer()
    {
        if (string.IsNullOrWhiteSpace(Key) || string.IsNullOrWhiteSpace(Region))
        {
            Logger.Warning("<AzureTTS> Key and Region must be entered");
            Renderer.Render();
            return;
        }

        Task.Run(async () =>
        {
            try
            {
                if (_synthesizer is not null)
                {
                    await _synthesizer.StopSpeakingAsync();
                    _synthesizer.Dispose();
                    _synthesizer = null;
                }

                _conf = SpeechConfig.FromSubscription(Key, Region);
                _synthesizer = new SpeechSynthesizer(_conf);
            }
            catch (Exception e)
            {
                Logger.Error("<AzureTTS> In Reset Synthesizer: " + e.Message);
                Renderer.Render();
                return;
            }
        });
    }

    internal static async Task<ReadOnlyCollection<VoiceInfo>> GetVoices()
    {
        if (_synthesizer is null) return new ReadOnlyCollection<VoiceInfo>(new Collection<VoiceInfo>());
        else return (await _synthesizer.GetVoicesAsync()).Voices;
    }

    internal static void InterruptAndRead(string text, VoiceInfo voice, int volume, int pitch, double rate)
    {
        Task.Run(async () =>
        {
            if (_synthesizer is null) return;
            await _synthesizer.StopSpeakingAsync();

            try
            {
                string ssml = $@"
                    <speak version=""1.0"" xmlns=""http://www.w3.org/2001/10/synthesis"" xml:lang=""{voice.Locale}"">
                        <voice name=""{voice.ShortName}"">
                            <prosody volume=""{volume}"" pitch=""{(pitch < 0 ? "" : "+")}{pitch}Hz"" rate=""{rate.ToString("0.##").Replace(",", ".")}"">
                                {text}
                            </prosody>
                        </voice >
                    </speak>";

                await _synthesizer.SpeakSsmlAsync(ssml);
            }
            catch (Exception e)
            {
                Logger.Error("<AzureTTS> " + e.Message);
                Renderer.Render();
            }
        });
    }

    internal static void AddTextToRead(string username, string text)
    {
        if (string.IsNullOrWhiteSpace(Key))
            Logger.Warning("<AzureTTS> Key is empty, enter it to use TTS");
        else if (string.IsNullOrWhiteSpace(Region))
            Logger.Warning("<AzureTTS> Region is empty, enter it to use TTS");
        else
            lock (_textToRead)
                _textToRead.Add((username, text));
    }

    internal static void SetDefaultVoice(VoiceInfo voice, int volume, int pitch, double rate)
    {
        DefaultVoice = voice.ShortName;
        DefaultVoiceVolume = Math.Clamp(volume, 0, 100).ToString();
        DefaultVoicePitch = Math.Clamp(pitch, -100, 100).ToString();
        DefaultVoiceRate = Math.Clamp(rate, 0.25, 2).ToString("0.##").Replace(",", ".");
    }

    internal static void SetVoice(string username, VoiceInfo voice, int volume, int pitch, double rate)
    {
        lock (UserVoices)
        {
            if (UserVoices.ContainsKey(username))
                UserVoices[username] = UserVoices[username] with
                {
                    voice = voice.ShortName,
                    volume = volume,
                    pitch = pitch,
                    rate = rate
                };
            else UserVoices[username] = new(username, voice.ShortName, volume, pitch, rate);

            Settings.SetArr("AzureSpeechUserVoices", GetUserVoices());
        }
    }

    internal static void ResetVoice(string username)
    {
        lock (UserVoices)
            if (UserVoices.ContainsKey(username))
                UserVoices.Remove(username);
    }

    internal static string[] GetUserVoices()
        => UserVoices.Values.Select(i => $"{i.username}\n{i.voice}\n{i.volume}\n{i.pitch}\n{i.rate?.ToString()?.Replace(",", ".")}").ToArray();

    internal static void ParseUserVoices(string[] voices)
    {
        UserVoices.Clear();
        foreach (var item in voices)
        {
            var lines = item.Split('\n');
            if (lines.Length != 5) continue;
            if (string.IsNullOrWhiteSpace(lines[0])) continue;
            if (string.IsNullOrWhiteSpace(lines[1])) continue;
            int? volume = int.TryParse(lines[2], out int num1) ? num1 : 0;
            int? pitch = int.TryParse(lines[3], out int num2) ? num1 : 0;
            double? rate = double.TryParse(lines[4], NumberStyles.Any, CultureInfo.InvariantCulture, out var num3) ? num3 : 1;

            var info = new UserVoiceInfo(lines[0], lines[1], volume, pitch, rate);
            UserVoices[lines[0]] = info;
        }
    }
}
