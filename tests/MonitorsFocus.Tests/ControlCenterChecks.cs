using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Forms;
using MonitorsFocus;
using MonitorsFocus.Brightness;
using MonitorsFocus.Core;
using MonitorsFocus.Monitoring;
using MonitorsFocus.Settings;

internal static class ControlCenterChecks
{
    private static ControlSettings Settings() => new()
    {
        Language = "en", ScheduleEnabled = true, KeepSecondaryAwakeOnFullscreen = false,
        Scenes =
        [
            new() { Id = "day", Name = "Day", Behavior = new() { FocusEnabled = true }, Targets = new() { ["a"] = new(90, null) } },
            new() { Id = "night", Name = "Night", Behavior = new() { FocusEnabled = true }, Targets = new() { ["a"] = new(10, 2700) } }
        ],
        Schedule = [new(8 * 60, "day", 0), new(20 * 60, "night", 60)]
    };

    public static async Task ManualHandoffAsync()
    {
        await using var app = new Fixture(Settings(), new(2026, 11, 1, 19, 59, 59));
        app.Tick();
        app.Send(new("temperature", Value: 4500));
        app.Send(new("brightness", "a", 5));
        app.Now = new(2026, 11, 1, 20, 0, 0);
        app.Tick();
        Equal(5, app.Snapshot().Displays[0].Brightness);
        Equal(4500, app.Snapshot().Displays[0].Kelvin);
        app.Now = app.Now.AddMinutes(30);
        app.Tick();
        Equal(8, app.Snapshot().Displays[0].Brightness);
        Equal(3600, app.Snapshot().Displays[0].Kelvin);
        app.Send(new("resume-plan"));
        Equal(50, app.Snapshot().Displays[0].Brightness); // Explicit resume uses the clock position.
        Equal(4600, app.Snapshot().Displays[0].Kelvin);
    }

    public static async Task MissedNodeAsync()
    {
        await using var app = new Fixture(Settings(), new(2026, 11, 1, 19, 59, 59));
        app.Tick();
        app.Send(new("brightness", "a", 5));
        app.Now = new(2026, 11, 1, 20, 30, 0); // Resume after a missed node; do not replay its fade.
        app.Tick();
        Equal(50, app.Snapshot().Displays[0].Brightness);
        Equal(4600, app.Snapshot().Displays[0].Kelvin);
    }

    public static async Task UnrelatedSaveAsync()
    {
        await using var app = new Fixture(Settings(), new(2026, 11, 1, 19, 0, 0));
        app.Tick();
        var saved = app.Snapshot().Settings;
        saved.Language = "zh-CN";
        saved.FocusEnabled = false;
        saved.Scenes[0].Name = "Renamed";
        app.Save(saved);
        app.Tick();
        var snapshot = app.Snapshot();
        Check(!snapshot.Settings.FocusEnabled);
        Equal(saved.Revision, snapshot.Settings.Revision); // No second save from replayed scene rules.
        Equal("时间计划 · Renamed", snapshot.Source);
        app.Send(new("brightness", "a", 33));
        saved = app.Snapshot().Settings;
        saved.Language = "en";
        app.Save(saved);
        app.Tick();
        Equal(33, app.Snapshot().Displays[0].Brightness);
        Check(app.Snapshot().Source.StartsWith("Manual"));
        app.Now = new(2026, 11, 1, 20, 0, 0);
        app.Tick();
        Check(app.Snapshot().Settings.FocusEnabled); // Next node applies its rules once.
    }

    public static async Task PlanEditAsync()
    {
        await using var app = new Fixture(Settings(), new(2026, 11, 1, 19, 0, 0));
        app.Tick();
        var saved = app.Snapshot().Settings;
        saved.Scenes[0].Targets["a"] = new(44, null);
        saved.Scenes[0].Behavior!.FocusEnabled = false;
        app.Save(saved);
        app.Tick();
        Equal(44, app.Snapshot().Displays[0].Brightness);
        Check(!app.Snapshot().Settings.FocusEnabled);
        saved = app.Snapshot().Settings;
        saved.ScheduleEnabled = false;
        app.Save(saved);
        app.Tick();
        Equal("Manual adjustment", app.Snapshot().Source);
        app.Send(new("scene", "night"));
        Equal("Scene · Night", app.Snapshot().Source);
        Equal(10, app.Snapshot().Displays[0].Brightness);
        Check(app.Snapshot().Settings.FocusEnabled);
    }

    public static async Task OfflineBindingAsync()
    {
        var weights = new Dictionary<string, double> { ["a"] = 40, ["b"] = 60, ["offline"] = 1000 };
        var result = RatioBinding.CalculateAvailable(weights, ["a", "b"], "a", 40);
        Equal(40, result["a"]);
        Equal(60, result["b"]);
        Equal(100, RatioBinding.CalculateAvailable(weights, ["a"], "a", 100)["a"]);
        var settings = Settings();
        settings.ScheduleEnabled = false;
        settings.Linked = true;
        settings.LinkWeights = new() { ["a"] = 40, ["offline"] = 1000 };
        await using var app = new Fixture(settings, new(2026, 11, 1));
        var snapshot = app.Send(new("brightness", "b", 77));
        Check(snapshot.Error == null);
        Equal(77, snapshot.Displays.Single(d => d.Id == "b").Brightness);
        Equal(60, snapshot.Displays.Single(d => d.Id == "a").Brightness);
        snapshot = app.Send(new("brightness", "a", 80));
        Equal(80, snapshot.Displays.Single(d => d.Id == "a").Brightness);
        Equal(1000.0, snapshot.Settings.LinkWeights["offline"]); // Preserve the reconnect ratio.
        var linked = snapshot.Settings;
        linked.LinkWeights = weights;
        app.Save(linked);
        snapshot = app.Send(new("brightness", "a", 0));
        Check(snapshot.Displays.All(d => d.Brightness == 0));
        snapshot = app.Send(new("brightness", "a", 20));
        Equal(20, snapshot.Displays.Single(d => d.Id == "a").Brightness);
        Equal(30, snapshot.Displays.Single(d => d.Id == "b").Brightness);
        snapshot = app.Send(new("brightness", "a", 100));
        Equal(67, snapshot.Displays.Single(d => d.Id == "a").Brightness);
        Equal(100, snapshot.Displays.Single(d => d.Id == "b").Brightness);
        Check(!app.Send(new("link", Value: 0)).Settings.Linked);
    }

    public static Task SettingsRoundTripAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), "MonitorsFocus-roundtrip-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new ControlSettingsStore(directory);
            foreach (var language in new[] { "zh-CN", "en", "system" })
            {
                var settings = Settings();
                settings.Language = language;
                settings.Linked = true;
                settings.LinkWeights = new() { ["a"] = 40, ["b"] = 60 };
                settings.ControlledDisplayIds = new() { "b", "offline" };
                settings.FocusEnabled = true;
                settings.FocusDelaySeconds = 27;
                settings.FocusBrightness = 7;
                settings.KeepSecondaryAwakeOnFullscreen = false;
                settings.LaunchOnStartup = true;
                settings.Scenes[0].Name = "用户名字 {0}";
                settings.Scenes[0].Behavior = SceneBehavior.Capture(settings);
                store.Save(settings);
                var loaded = store.Load();
                Equal(JsonSerializer.Serialize(settings), JsonSerializer.Serialize(loaded));
                Equal("用户名字 {0}", Localization.SceneName(loaded.Scenes[0], language));
                Check(!JsonSerializer.Serialize(loaded).Contains("Hotkey", StringComparison.OrdinalIgnoreCase));
            }
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
        return Task.CompletedTask;
    }

    public static async Task FocusPlanFullscreenAsync()
    {
        var settings = Settings();
        settings.FocusDelaySeconds = 0;
        settings.Scenes[0].Targets["b"] = new(60, null);
        settings.Scenes[1].Targets["b"] = new(30, null);
        await using var app = new Fixture(settings, new(2026, 11, 1, 19, 0, 0));
        app.Tick();
        Equal(10, await app.EffectiveBrightness("b"));
        Equal(90, await app.EffectiveBrightness("a")); // Primary is never dimmed.
        app.Now = new(2026, 11, 1, 20, 30, 0);
        app.Tick();
        Equal(45, app.Snapshot().Displays.Single(d => d.Id == "b").Brightness);
        Equal(10, await app.EffectiveBrightness("b"));
        var saved = app.Snapshot().Settings;
        saved.KeepSecondaryAwakeOnFullscreen = true;
        app.Save(saved);
        app.Fullscreen("b");
        app.Tick();
        Equal(45, await app.EffectiveBrightness("b")); // Restore the changing plan baseline.
        app.Fullscreen();
        app.Tick();
        Equal(10, await app.EffectiveBrightness("b"));
        app.Send(new("pause"));
        Equal(45, await app.EffectiveBrightness("b"));
        app.Send(new("pause"));
        Equal(10, await app.EffectiveBrightness("b"));
        saved = app.Snapshot().Settings;
        saved.Scenes[1].Behavior!.ControlledDisplayIds = new();
        app.Save(saved);
        app.Send(new("scene", "night"));
        Equal(30, await app.EffectiveBrightness("b"));
        Check(app.Snapshot().Settings.FocusEnabled && app.Snapshot().Settings.ControlledDisplayIds!.Count == 0);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly ControlCenterContext _app = (ControlCenterContext)RuntimeHelpers.GetUninitializedObject(typeof(ControlCenterContext));
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "MonitorsFocus-integration-" + Guid.NewGuid().ToString("N"));
        private readonly ControlSettingsStore _store;
        private readonly List<ControlledDisplay> _controllers = new();
        private readonly Dictionary<string, FakeColorDevice> _devices = new();
        private readonly System.Windows.Forms.Timer _timer = new();
        public DateTime Now;

        public Fixture(ControlSettings settings, DateTime now)
        {
            Now = now;
            _store = new(_directory);
            Set("_store", _store);
            Set("_settings", settings);
            Set("_clock", (Func<DateTime>)(() => Now));
            Set("_timer", _timer);
            Set("_fullscreenDisplays", new HashSet<string>());
            Set("_retainedKelvin", new Dictionary<string, int?>());
            var displayType = typeof(ControlCenterContext).GetNestedType("Display", BindingFlags.NonPublic)!;
            var displays = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(displayType))!;
            foreach (var id in new[] { "a", "b" })
            {
                var display = Activator.CreateInstance(displayType, nonPublic: true)!;
                var device = new FakeColorDevice();
                _devices[id] = device;
                var controller = new ControlledDisplay(device);
                _controllers.Add(controller);
                void Field(string name, object value) => displayType.GetField(name)!.SetValue(display, value);
                Field("Id", id);
                Field("Name", id);
                var monitor = new MonitorInfo(Screen.PrimaryScreen!, IntPtr.Zero);
                typeof(MonitorInfo).GetField("<Id>k__BackingField", Flags)!.SetValue(monitor, id);
                typeof(MonitorInfo).GetField("<IsPrimary>k__BackingField", Flags)!.SetValue(monitor, id == "a");
                // Keep the real pointer outside fake secondary coordinates, without moving it.
                typeof(MonitorInfo).GetField("<Bounds>k__BackingField", Flags)!.SetValue(monitor, new System.Drawing.Rectangle(-100000, -100000, 100, 100));
                Field("Monitor", monitor);
                Field("Controller", controller);
                Field("Brightness", 60);
                displays.Add(display);
            }
            Set("_displays", displays);
        }

        private void Set(string name, object value) => typeof(ControlCenterContext).GetField(name, Flags)!.SetValue(_app, value);
        private object? Invoke(string name, params object?[] args) => typeof(ControlCenterContext).GetMethod(name, Flags)!.Invoke(_app, args);
        public void Tick() { Set("_nextPlanAt", 0L); Invoke("Tick"); }
        public ControlSnapshot Snapshot() => (ControlSnapshot)Invoke("Snapshot", new object?[] { null })!;
        public ControlSnapshot Send(ControlRequest request) => (ControlSnapshot)Invoke("Handle", request)!;
        public void Fullscreen(params string[] ids)
        {
            Set("_fullscreenDisplays", ids.ToHashSet());
            Set("_nextFullscreenAt", long.MaxValue); // Inject a detector result; no desktop enumeration.
        }
        public async Task<int> EffectiveBrightness(string id)
        {
            var controller = _controllers[id == "a" ? 0 : 1];
            var worker = (Task)typeof(ControlledDisplay).GetField("_worker", Flags)!.GetValue(controller)!;
            await worker;
            return _devices[id].Brightness;
        }
        public void Save(ControlSettings settings)
        {
            // Exercise the post-persistence path without touching the user's startup registry.
            _store.Save(settings);
            Invoke("ApplySavedSettings", settings);
        }
        public async ValueTask DisposeAsync()
        {
            _timer.Dispose();
            foreach (var controller in _controllers) await controller.CloseAsync();
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        }
    }

    private static void Check(bool condition) { if (!condition) throw new Exception("Control center integration assertion failed."); }
    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}.");
    }
}
