using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Windows.Forms;
using MonitorsFocus.Brightness;
using MonitorsFocus.Monitoring;
using MonitorsFocus.UI;
using MonitorsFocus.Core;
using MonitorsFocus.Settings;

internal static class LocalizationChecks
{
    public static void Run()
    {
        using var resources = typeof(Localization).Assembly.GetManifestResourceStream("MonitorsFocus.Core.strings.json")!;
        foreach (var (key, value) in JsonSerializer.Deserialize<Dictionary<string, string>>(resources)!)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new Exception($"Empty translation: {key}");
            Equal(string.Join(',', Regex.Matches(key, @"\{\d+\}").Select(m => m.Value).Order()),
                string.Join(',', Regex.Matches(value, @"\{\d+\}").Select(m => m.Value).Order()));
        }
        CheckErrorCoverage();
        Equal("zh-CN", Localization.Resolve("system", CultureInfo.GetCultureInfo("zh-TW")));
        Equal("en", Localization.Resolve("system", CultureInfo.GetCultureInfo("fr-FR")));
        Equal("en", Localization.Resolve("en", CultureInfo.GetCultureInfo("zh-CN")));
        Equal("zh-CN", Localization.Resolve("zh-CN", CultureInfo.GetCultureInfo("en-US")));
        Equal("Detecting displays…", Localization.Text("正在检测显示器…", "en"));
        Equal("Detection failed: Cannot read monitor brightness.", Localization.Message("检测失败：Cannot read monitor brightness.", "en"));
        Equal("检测失败：无法读取显示器亮度。", Localization.Message("Detection failed: Cannot read monitor brightness.", "zh-CN"));
        Equal("driver detail 0x1234", Localization.Message("driver detail 0x1234", "en"));
        var scene = new Scene { Name = "日常", DefaultName = "日常" };
        Equal("Day", Localization.SceneName(scene, "en"));
        scene.DefaultName = null; // User-authored names that match a resource key stay intact.
        Equal("日常", Localization.SceneName(scene, "en"));
        Equal("Schedule · 日常 {0}", Localization.Format("时间计划 · {0}", "en", "日常 {0}"));

        var directory = Path.Combine(Path.GetTempPath(), "MonitorsFocus-i18n-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "control-center.json");
            File.WriteAllText(path, "{}");
            var store = new ControlSettingsStore(directory);
            var settings = store.Load();
            Equal("system", settings.Language);
            settings.Language = "en";
            settings.Scenes.Add(scene);
            store.Save(settings);
            Equal("en", store.Load().Language);
            Equal("日常", store.Load().Scenes[0].Name);
            settings.Language = "invalid";
            try { store.Save(settings); throw new Exception("Invalid language accepted"); }
            catch (ArgumentException) { }
            Equal("en", store.Load().Language);
            foreach (var language in new[] { UiLanguage.Chinese, UiLanguage.English, UiLanguage.System })
            {
                File.Delete(path);
                File.WriteAllText(Path.Combine(directory, "settings.json"), JsonSerializer.Serialize(new AppSettings { Language = language }));
                Equal(language.Preference(), store.Load().Language);
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
        CheckLegacyEditor();
    }

    private static void CheckErrorCoverage()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "MonitorsFocus.sln"))) root = root.Parent;
        if (root == null) throw new Exception("Repository not found for application error coverage.");
        var count = 0;
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root.FullName, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)))
        {
            foreach (Match match in Regex.Matches(File.ReadAllText(file), "throw new \\w*Exception\\([^;\\r\\n]*?\"([^\"\\r\\n]*)\""))
            {
                var text = match.Groups[1].Value;
                if (string.IsNullOrWhiteSpace(text) || text.Contains('{')) continue;
                var language = Regex.IsMatch(text, "[\\p{IsCJKUnifiedIdeographs}]") ? "en" : "zh-CN";
                if (Localization.Message(text, language) == text)
                    throw new Exception($"Untranslated application error in {Path.GetFileName(file)}: {text}");
                count++;
            }
        }
        if (count == 0) throw new Exception("No application errors checked.");
        Console.WriteLine($"  {count} application-owned error literals have bilingual resources.");
    }

    private static void CheckLegacyEditor()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                var monitors = Screen.AllScreens.Select(s => new MonitorInfo(s, IntPtr.Zero)).ToArray();
                using var form = new SettingsForm(new AppSettings { Language = UiLanguage.English }, monitors,
                    new Dictionary<string, HardwareDimmer.Capability>());
                T Field<T>(string name) => (T)typeof(SettingsForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
                Field<ComboBox>("_dimmingModeCombo").SelectedIndex = 2;
                Field<NumericUpDown>("_delayInput").Value = 123;
                var list = Field<CheckedListBox>("_monitorList");
                list.SetItemChecked(0, true);
                Field<ComboBox>("_languageCombo").SelectedIndex = 2;
                Equal("仅硬件", Field<ComboBox>("_dimmingModeCombo").Text);
                Equal("123", Field<NumericUpDown>("_delayInput").Value.ToString());
                if (!list.GetItemChecked(0)) throw new Exception("Language switch lost monitor selection");
                Equal("Monitors Focus 设置", form.Text);
                Field<ComboBox>("_languageCombo").SelectedIndex = 1;
                Equal("Hardware only", Field<ComboBox>("_dimmingModeCombo").Text);
                form.Close();
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) throw failure;
    }

    private static void Equal(string expected, string? actual)
    {
        if (actual != expected) throw new Exception($"Expected '{expected}', got '{actual}'");
    }
}
