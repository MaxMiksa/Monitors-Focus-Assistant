using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MonitorsFocus.Settings;

internal sealed class SettingsStore
{
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public SettingsStore(string settingsPath)
    {
        _settingsPath = settingsPath;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    public AppSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return CreateDefault();
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? CreateDefault();
            settings.Normalize();
            return settings;
        }
        catch
        {
            return CreateDefault();
        }
    }

    public void Save(AppSettings settings)
    {
        settings.Normalize();
        var folder = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        File.WriteAllText(_settingsPath, json);
    }

    private static AppSettings CreateDefault()
    {
        var defaults = new AppSettings();
        defaults.Normalize();
        return defaults;
    }
}
