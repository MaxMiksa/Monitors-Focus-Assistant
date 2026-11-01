using System.Globalization;
using System.Text.Json;

namespace MonitorsFocus.Core;

public static class Localization
{
    private static readonly Dictionary<string, string> English = JsonSerializer.Deserialize<Dictionary<string, string>>(
        typeof(Localization).Assembly.GetManifestResourceStream("MonitorsFocus.Core.strings.json")!)!;
    private static readonly Dictionary<string, string> Chinese = English.GroupBy(x => x.Value)
        .ToDictionary(x => x.Key, x => x.First().Key);
    // Only application-owned error wrappers contain dynamic details. Device names and user text
    // are formatted separately, never searched or translated as messages.
    private static readonly string[] ErrorTemplates = { "原配置未覆盖：{0}", "检测失败：{0}", "显示器恢复失败：{0}",
        "恢复失败：{0}", "WMI 亮度调节失败：{0}", "保存设置失败：{0}", "硬件刷新失败：{0}", "时间计划未应用：{0}" };

    public static string Resolve(string? preference, CultureInfo? systemCulture = null) => preference switch
    {
        "zh-CN" => "zh-CN",
        "en" => "en",
        _ => (systemCulture ?? CultureInfo.CurrentUICulture).TwoLetterISOLanguageName == "zh" ? "zh-CN" : "en"
    };

    public static string Text(string key, string? language) => Resolve(language) == "zh-CN" ? key : English.GetValueOrDefault(key, key);

    public static string Format(string key, string? language, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, Text(key, language), args);

    public static string? Message(string? message, string? language)
    {
        if (message == null) return null;
        if (English.ContainsKey(message)) return Text(message, language);
        if (Chinese.TryGetValue(message, out var key)) return Text(key, language);
        foreach (var template in ErrorTemplates)
        {
            foreach (var source in new[] { template, English[template] })
            {
                var prefix = source[..source.IndexOf("{0}", StringComparison.Ordinal)];
                if (message.StartsWith(prefix, StringComparison.Ordinal))
                    return Format(template, language, Message(message[prefix.Length..], language));
            }
        }
        // OS and driver diagnostics retain their original text.
        return message;
    }

    public static string SceneName(Scene scene, string? language) =>
        scene.DefaultName != null && scene.Name == scene.DefaultName ? Text(scene.DefaultName, language) : scene.Name;

    public static string ReadPreference()
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Monitors-Focus", "control-center.json");
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.TryGetProperty("Language", out var language) ? language.GetString() ?? "system" : "system";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        { return "system"; }
    }
}
