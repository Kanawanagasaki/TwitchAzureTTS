namespace TwitchAzureTTS;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

internal static class Settings
{
    private static string _filePath;
    private static Dictionary<string, string> _kv = new();
    private static Dictionary<string, string[]> _kvArr = new();

    static Settings()
    {
        var appdataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dirPath = Path.Combine(appdataDir, "TwitchAzureTTS");
        if(!Directory.Exists(dirPath))
            Directory.CreateDirectory(dirPath);
        _filePath = Path.Combine(dirPath, "settings.json");
        if(File.Exists(_filePath))
        {
            var json = File.ReadAllText(_filePath);
            var obj = JsonConvert.DeserializeObject<JObject>(json);
            if(obj is not null)
                foreach(var prop in obj.Properties())
                {
                    var propName = prop.Name;
                    if(prop.Value.Type == JTokenType.Array)
                    {
                        _kvArr[propName] = new string[prop.Value.Count()];
                        for (int i = 0; i < _kvArr[propName].Length; i++)
                            _kvArr[propName][i] = prop?.Value[i]?.ToString() ?? "";
                    }
                    else
                        _kv[propName] = prop?.Value?.ToString() ?? "";
                }
        }
    }

    internal static string Get(string name, string? defaultVal = null)
    {
        if(_kv.ContainsKey(name))
            return _kv[name];

        Set(name, defaultVal ?? "");
        return _kv[name];
    }

    internal static void Set(string name, string value)
    {
        lock (_kv)
            _kv[name] = value;
        Save();
    }

    internal static string[] GetArr(string name)
    {
        if (_kvArr.ContainsKey(name))
            return _kvArr[name];

        SetArr(name, Array.Empty<string>());
        return _kvArr[name];
    }

    internal static void SetArr(string name, string[] value)
    {
        lock (_kv)
            _kvArr[name] = value;
        Save();
    }

    internal static void Save()
    {
        var obj = new Dictionary<string, object>();
        foreach (var kv in _kv) obj[kv.Key] = kv.Value;
        foreach (var kv in _kvArr) obj[kv.Key] = kv.Value;
        var json = JsonConvert.SerializeObject(obj);
        File.WriteAllText(_filePath, json);
    }
}
