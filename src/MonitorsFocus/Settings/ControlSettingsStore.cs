using System.Text.Json;
using MonitorsFocus.Core;

namespace MonitorsFocus.Settings;

internal sealed class ControlSettingsStore
{
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Monitors-Focus", "control-center.json");

    public ControlSettings Load()
    {
        if (!File.Exists(_path)) return new ControlSettings();
        var settings = JsonSerializer.Deserialize<ControlSettings>(File.ReadAllText(_path))
            ?? throw new InvalidDataException("配置为空。");
        DailyPlan.Validate(settings);
        return settings;
    }

    public void Save(ControlSettings settings)
    {
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
