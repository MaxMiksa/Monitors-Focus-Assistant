using System.Text.Json;
using System.Text.Json.Serialization;
using MonitorsFocus.Core;

namespace MonitorsFocus.Settings;

internal sealed class ControlSettingsStore
{
    private readonly string _path;

    public ControlSettingsStore(string? directory = null)
    {
        _path = Path.Combine(directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Monitors-Focus"), "control-center.json");
    }

    public ControlSettings Load()
    {
        if (!File.Exists(_path)) return MigrateLegacy();
        var settings = JsonSerializer.Deserialize<ControlSettings>(File.ReadAllText(_path))
            ?? throw new InvalidDataException("配置为空。");
        DailyPlan.Validate(settings);
        foreach (var scene in settings.Scenes) scene.Behavior ??= SceneBehavior.Capture(settings);
        return settings;
    }

    private ControlSettings MigrateLegacy()
    {
        var legacyPath = Path.Combine(Path.GetDirectoryName(_path)!, "settings.json");
        if (!File.Exists(legacyPath)) return new ControlSettings();
        var legacy = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(legacyPath),
            new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } })
            ?? throw new InvalidDataException("旧配置为空，未迁移。");
        legacy.Normalize();
        var settings = new ControlSettings
        {
            LaunchOnStartup = legacy.LaunchOnStartup,
            Language = legacy.Language.Preference(),
            FocusDelaySeconds = legacy.DelaySeconds,
            FocusBrightness = legacy.HardwareDimLevel
        };
        var backup = legacyPath + ".pre-center.bak";
        if (!File.Exists(backup)) File.Copy(legacyPath, backup);
        Save(settings);
        return settings;
    }

    public void Save(ControlSettings settings)
    {
        foreach (var scene in settings.Scenes) scene.Behavior ??= SceneBehavior.Capture(settings);
        DailyPlan.Validate(settings);
        settings.Revision++;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporary = _path + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            if (File.Exists(_path)) File.Replace(temporary, _path, _path + ".bak");
            else File.Move(temporary, _path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
