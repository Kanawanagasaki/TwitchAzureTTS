using Azure.ResourceManager.Resources;
using Microsoft.CognitiveServices.Speech;
using System.Globalization;

namespace TwitchAzureTTS;

internal static class Menu
{
    internal static MenuColumn[] Columns { get; private set; } = new MenuColumn[]
    {
        new ("Twitch", new MenuItem[]
        {
            new( () => TwitchChat.Status == TwitchChat.Statuses.Disconnected
                        ? "Connect"
                        : TwitchChat.Status != TwitchChat.Statuses.Connected
                        ? "Pending"
                        : "Disconnect", TwitchConnect, 0 ),
            new( () => "Set Channel", TwitchSetChannel, 0 ),
            new( ( )=> TwitchTokenKeeper.IsLoggined ? "Logout" : "Login", TwitchLogin, 0 )
        }),
        new("Azure TTS", new MenuItem[]
        {
            new(() => "Set Key", AzureSetKey, 0),
            new(() => "Set Region", AzureSetRegion, 0),
            new(() => "Set Default Voice", AzureSetDefaultVoice, 0),
            new(() => "Set Viewer's Voice", AzureSetViewerVoice, 0),
            new(() => "Reload Synthesizer", AzureTts.ReloadSynthesizer, 0),
            new(() => "Set Tenant Id", AzureTenantId, 1),
            new(() => "Select Resource", AzureSelectResource, 1),
            new(() => "Metrics, Login", AzureMetrics.LogIn, 1),
            new(() => "Metrics, Update", AzureMetrics.UpdateMetrics, 1)
        }),
        new("App", new MenuItem[] { new( () => "Exit", Exit, 0) })
    };

    internal static TextInput? Input { get; private set; } = null;
    internal static RangeInput? Range { get; private set; } = null;
    internal static SelectInput? Select { get; private set; } = null;

    internal static bool IsRunnings = true;

    internal static int Column { get; private set; } = 0;
    internal static int Offset { get; private set; } = 0;
    internal static int Row { get; private set; } = 0;
    internal static int MaxRows => Columns.Max(i => i.Items.GroupBy(i => i.Offset).Max(g => g.Count()));

    private static void TwitchLogin()
    {
        if (TwitchTokenKeeper.IsLoggined)
            TwitchTokenKeeper.LogOut();
        else TwitchTokenKeeper.Login();
    }

    private static void TwitchConnect()
    {
        if (TwitchChat.Status == TwitchChat.Statuses.Disconnected)
            Task.Run(async () =>
            {
                await TwitchChat.Connect();
                Renderer.Render();
            });
        else TwitchChat.Disconnect();
    }

    private static void TwitchSetChannel()
    {
        if (TwitchChat.Status == TwitchChat.Statuses.Disconnected)
        {
            Input = new TextInput("Set Channel", TwitchChat.Channel, i =>
            {
                TwitchChat.Channel = i.Input;
            });
        }
    }

    private static void AzureSetKey()
    {
        Input = new TextInput("Azure Speech Service, Set Key", AzureTts.Key, i =>
        {
            AzureTts.Key = i.Input;
        })
        { IsPassword = true };
    }

    private static void AzureSetRegion()
    {
        Input = new TextInput("Azure Speech Service, Set Region", AzureTts.Region, i =>
        {
            AzureTts.Region = i.Input;
        });
    }

    private static void AzureSetDefaultVoice()
    {
        Task.Run(async () =>
        {
            await SetVoice((voice, volume, pitch, rate) =>
            {
                AzureTts.SetDefaultVoice(voice, volume, pitch, rate);

                Logger.Info($"<AzureTTS> Default voice updated" +
                    $"\nName: {AzureTts.DefaultVoice}" +
                    $"\nVolume: {AzureTts.DefaultVoiceVolume}" +
                    $"\nPitch: {AzureTts.DefaultVoicePitch}Hz" +
                    $"\nRate: {AzureTts.DefaultVoiceRate}");
            });
        });
    }

    private static void AzureSetViewerVoice()
    {
        var voicesTask = AzureTts.GetVoices();

        Input = new TextInput("Type viewer's username (not displayname)", "",
            username =>
            {
                Task.Run(async () =>
                {
                    await SetVoice((voice, volume, pitch, rate) =>
                    {
                        AzureTts.SetVoice(username.Input, voice, volume, pitch, rate);

                        Logger.Info($"<AzureTTS> {username.Input}'s voice updated" +
                            $"\nName: {voice.ShortName}" +
                            $"\nVolume: {volume}" +
                            $"\nPitch: {pitch}Hz" +
                            $"\nRate: {rate.ToString("0.##").Replace(",", ".")}");
                    });
                });
            });
    }

    private static async Task SetVoice(Action<VoiceInfo, int, int, double> callback)
    {
        var voices = await AzureTts.GetVoices();

        Select = new("Select voice")
        {
            IsExpanded = true,
            Childs = voices.GroupBy(v => v.Locale.Substring(0, 2).ToUpper()).Select(
                g1 => new SelectInput(g1.Key)
                {
                    Childs = g1.GroupBy(v => v.Locale).Select(
                        g2 => new SelectInput(g2.Key)
                        {
                            Items = g2.Select(v => KeyValuePair.Create($"{v.ShortName.Replace(v.Locale, "").Replace("-", "").Replace("Neural", "")} - {v.Gender}", v as object)).ToArray()
                        }).ToArray()
                }).ToArray(),
            OnSelect = selection =>
            {
                if (selection is not VoiceInfo voice) return false;
                Range = new RangeInput("Volume level of the speaking voice", int.TryParse(AzureTts.DefaultVoiceVolume, out int defaultVolume) ? defaultVolume : 50,
                volumeRange =>
                {
                    Range = new RangeInput("Baseline pitch for the text", int.TryParse(AzureTts.DefaultVoicePitch, out int defaultPitch) ? defaultPitch : 0,
                    pitchRange =>
                    {
                        Range = new RangeInput("Speaking rate of the text", double.TryParse(AzureTts.DefaultVoiceRate, NumberStyles.Any, CultureInfo.InvariantCulture, out double defaultRate) ? defaultRate : 1,
                        rateRange =>
                        {

                            int volume = (int)volumeRange.Value;
                            int pitch = (int)pitchRange.Value;
                            double rate = rateRange.Value;

                            callback(voice, volume, pitch, rate);

                            return true;
                        })
                        {
                            MinValue = 0.25,
                            MaxValue = 2,
                            Step = 0.05,
                            OnChange = r => AzureTts.InterruptAndRead(
                                "Speaking rate of the text is " + r.Value.ToString("0.##").Replace(",", "."),
                                voice, (int)volumeRange.Value, (int)pitchRange.Value, r.Value)
                        };

                        return false;
                    })
                    {
                        MinValue = -100,
                        MaxValue = 100,
                        OnChange = r => AzureTts.InterruptAndRead(
                            "Baseline pitch for the text is " + r.Value, voice,
                            (int)volumeRange.Value, (int)r.Value, 1)
                    };

                    return false;
                })
                {
                    OnChange = r => AzureTts.InterruptAndRead(
                        "Volume level of the speaking voice is " + r.Value,
                        voice, (int)r.Value, 0, 1)
                };

                return true;
            },
            OnChange = selection =>
            {
                if (selection is not VoiceInfo voice) return;
                int defaultVolume = 50;
                int.TryParse(AzureTts.DefaultVoiceVolume, out defaultVolume);
                AzureTts.InterruptAndRead($"Hello, my name is {voice.LocalName}", voice, defaultVolume, 0, 1);
            }
        };
        Renderer.Render();
    }

    private static void AzureTenantId()
    {
        Input = new TextInput("Azure, Tenant Id", AzureMetrics.TenantId, i =>
        {
            AzureMetrics.TenantId = i.Input;
            AzureMetrics.LogIn();
            var resources = AzureMetrics.GetResources();
            if (resources is not null)
            {
                if (resources.Length == 1)
                {
                    var resourceId = resources?.First()?.Id ?? ""; // wth? resourceId still may be null here... even after `?? "";`
                    AzureMetrics.ResourceId = resourceId ?? "";
                }
                else if (resources.Length > 1)
                    AzureSelectResource(resources);
            }
        });
    }

    private static void AzureSelectResource()
    {
        var resources = AzureMetrics.GetResources();
        if (resources is not null)
            AzureSelectResource(resources);
    }

    private static void AzureSelectResource(GenericResourceData[] resources)
    {
        Select = new SelectInput("Azure, Select resource for statistics")
        {
            Items = resources.Select(r => KeyValuePair.Create(r.Name, r as object)).ToArray(),
            IsExpanded = true,
            OnSelect = kv =>
            {
                if (kv is not GenericResourceData resource) return false;
                AzureMetrics.ResourceId = resource.Id;
                return true;
            }
        };
    }

    private static void Exit()
    {
        IsRunnings = false;
    }

    internal static void OnKeyPress(ConsoleKeyInfo key)
    {
        if (Input is not null)
        {
            if (key.Key == ConsoleKey.Backspace)
            {
                if (Input.Input.Length > 0)
                    Input.Input = Input.Input.Substring(0, Input.Input.Length - 1);
            }
            else if (key.Key == ConsoleKey.Escape)
            {
                Input = null;
            }
            else if (key.Key == ConsoleKey.Enter)
            {
                Input.OnConfirm(Input);
                Input = null;
            }
            else Input.Input += key.KeyChar;
        }
        else if (Range is not null)
        {
            if (key.Key == ConsoleKey.LeftArrow)
            {
                var newVal = Math.Clamp(Range.Value - Range.Step, Range.MinValue, Range.MaxValue);
                if (newVal != Range.Value)
                {
                    Range.Value = newVal;
                    if (Range.OnChangeDebounce is not null)
                        Range.OnChangeDebounce(Range);
                }
            }
            else if (key.Key == ConsoleKey.RightArrow)
            {
                var newVal = Math.Clamp(Range.Value + Range.Step, Range.MinValue, Range.MaxValue);
                if (newVal != Range.Value)
                {
                    Range.Value = newVal;
                    if (Range.OnChangeDebounce is not null)
                        Range.OnChangeDebounce(Range);
                }
            }
            else if (key.Key == ConsoleKey.Escape)
                Range = null;
            else if (key.Key == ConsoleKey.Enter)
            {
                if (Range.OnConfirm(Range))
                    Range = null;
            }
        }
        else if (Select is not null)
        {
            var onChange = () =>
            {
                SelectInput deepest = Select;
                while (deepest.IsExpanded)
                {
                    if (deepest.SelectedIndex < deepest.Childs.Length && deepest.Childs[deepest.SelectedIndex].IsExpanded)
                        deepest = deepest.Childs[deepest.SelectedIndex];
                    else break;
                }

                var value = deepest.SelectedIndex < deepest.Childs.Length
                    ? deepest.Childs[deepest.SelectedIndex]
                    : deepest.Items[deepest.SelectedIndex - deepest.Childs.Length].Value;

                if (deepest.OnChange is not null)
                    deepest.OnChange(value);
                if (Select.OnChange is not null)
                    Select.OnChange(value);
            };

            if (key.Key == ConsoleKey.LeftArrow)
            {
                if (!Select.IsExpanded) return;

                SelectInput deepest = Select;
                while (true)
                {
                    if (deepest.SelectedIndex < deepest.Childs.Length && deepest.Childs[deepest.SelectedIndex].IsExpanded)
                        deepest = deepest.Childs[deepest.SelectedIndex];
                    else break;
                }
                if (deepest.IsExpanded)
                {
                    deepest.IsExpanded = false;
                    onChange();
                }
            }
            else if (key.Key == ConsoleKey.RightArrow)
            {
                SelectInput deepest = Select;
                while (deepest.IsExpanded)
                {
                    if (deepest.SelectedIndex < deepest.Childs.Length)
                        deepest = deepest.Childs[deepest.SelectedIndex];
                    else break;
                }
                if (!deepest.IsExpanded)
                {
                    deepest.IsExpanded = true;
                    onChange();
                }
            }
            else if (key.Key == ConsoleKey.UpArrow)
            {
                if (!Select.IsExpanded) return;

                SelectInput deepest = Select;
                while (deepest.IsExpanded)
                {
                    if (deepest.SelectedIndex < deepest.Childs.Length && deepest.Childs[deepest.SelectedIndex].IsExpanded)
                        deepest = deepest.Childs[deepest.SelectedIndex];
                    else break;
                }
                if (deepest.SelectedIndex > 0)
                {
                    deepest.SelectedIndex--;
                    onChange();
                }
            }
            else if (key.Key == ConsoleKey.DownArrow)
            {
                if (!Select.IsExpanded) return;

                SelectInput deepest = Select;
                while (deepest.IsExpanded)
                {
                    if (deepest.SelectedIndex < deepest.Childs.Length && deepest.Childs[deepest.SelectedIndex].IsExpanded)
                        deepest = deepest.Childs[deepest.SelectedIndex];
                    else break;
                }
                if (deepest.SelectedIndex < deepest.Childs.Length + deepest.Items.Length - 1)
                {
                    deepest.SelectedIndex++;
                    onChange();
                }
            }
            else if (key.Key == ConsoleKey.Escape)
            {
                Select = null;
            }
            else if (key.Key == ConsoleKey.Enter)
            {
                SelectInput deepest = Select;
                while (deepest.IsExpanded)
                {
                    if (deepest.SelectedIndex < deepest.Childs.Length && deepest.Childs[deepest.SelectedIndex].IsExpanded)
                        deepest = deepest.Childs[deepest.SelectedIndex];
                    else break;
                }

                if (Select.OnSelect?.Invoke(deepest.SelectedIndex < deepest.Childs.Length
                    ? deepest.Childs[deepest.SelectedIndex]
                    : deepest.Items[deepest.SelectedIndex - deepest.Childs.Length].Value) ?? false)
                    Select = null;
            }
        }
        else
        {
            int maxOffset = Columns[Column].Items.Max(i => i.Offset);
            if (key.Key == ConsoleKey.LeftArrow)
            {
                if (Offset == 0)
                {
                    if (Column > 0)
                    {
                        Column--;
                        Offset = Columns[Column].Items.Max(i => i.Offset);
                    }
                }
                else Offset--;
            }
            else if (key.Key == ConsoleKey.RightArrow)
            {
                if (Offset < maxOffset)
                    Offset++;
                else if (Column < Columns.Length - 1)
                {
                    Column++;
                    Offset = 0;
                }
            }
            else if (key.Key == ConsoleKey.UpArrow)
                Row--;
            else if (key.Key == ConsoleKey.DownArrow)
                Row++;

            Row = Math.Clamp(Row, 0, Columns[Column].Items.Where(i => i.Offset == Offset).Count() - 1);

            if (key.Key == ConsoleKey.Enter || key.Key == ConsoleKey.Spacebar)
                Columns[Column].Items.Where(i => i.Offset == Offset).ToList()[Row].OnSelect();
        }

        Renderer.Render();
    }

    internal static Action<T> Debounce<T>(this Action<T> func, int milliseconds = 1000)
    {
        CancellationTokenSource? cancelTokenSource = null;

        return arg =>
        {
            cancelTokenSource?.Cancel();
            cancelTokenSource = new CancellationTokenSource();

            Task.Delay(milliseconds, cancelTokenSource.Token)
                .ContinueWith(t =>
                {
                    if (t.IsCompletedSuccessfully)
                    {
                        func(arg);
                    }
                }, TaskScheduler.Default);
        };
    }

    internal record MenuColumn(string Name, MenuItem[] Items);
    internal record MenuItem(Func<string> Name, Action OnSelect, int Offset);
    internal class TextInput
    {
        internal string Title { get; init; }
        internal string Input { get; set; } = "";
        internal Action<TextInput> OnConfirm;
        internal bool IsPassword { get; set; } = false;

        internal TextInput(string title, string defaultInput, Action<TextInput> onConfirm)
            => (Title, Input, OnConfirm) = (title, defaultInput, onConfirm);
    }
    internal class RangeInput
    {
        internal string Title { get; init; }
        internal double Value { get; set; } = 0;
        internal double MinValue { get; set; } = 0;
        internal double MaxValue { get; set; } = 100;
        internal double Step { get; set; } = 1;
        internal Func<RangeInput, bool> OnConfirm;

        private Action<RangeInput>? _onChange = null;
        internal Action<RangeInput>? OnChange
        {
            get => _onChange;
            set
            {
                _onChange = value;
                if (_onChange is not null)
                    OnChangeDebounce = _onChange.Debounce();
            }
        }
        internal Action<RangeInput>? OnChangeDebounce = null;

        internal RangeInput(string title, double defaultValue, Func<RangeInput, bool> onConfirm)
            => (Title, Value, OnConfirm) = (title, defaultValue, onConfirm);
    }
    internal class SelectInput
    {
        internal string Title { get; init; }
        internal KeyValuePair<string, object>[] Items { get; set; } = Array.Empty<KeyValuePair<string, object>>();
        internal SelectInput[] Childs { get; set; } = Array.Empty<SelectInput>();
        internal Func<object, bool>? OnSelect { get; set; } = null;
        internal Action<object>? OnChange { get; set; } = null;

        internal bool IsExpanded = false;
        internal int SelectedIndex = 0;

        internal SelectInput(string title)
            => (Title) = (title);
    }
}
