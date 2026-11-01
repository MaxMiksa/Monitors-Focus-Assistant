using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using MonitorsFocus;
using MonitorsFocus.Brightness;
using MonitorsFocus.Core;
using MonitorsFocus.Monitoring;
using MonitorsFocus.Settings;

System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);

if (args.Contains("--window-fixture")) { WindowDetectionChecks.RunFixture(); return; }

if (args.Contains("--probe"))
{
    using var manager = new MonitorManager();
    foreach (var monitor in manager.Monitors)
    {
        var identity = DisplayIdentity.FindInstance(monitor.Id);
        Console.WriteLine($"Display={monitor.Id}; Primary={monitor.IsPrimary}; IdentityMapped={identity != null}");
        using var ddc = DdcBrightnessDevice.Open(monitor);
        using var wmi = WmiBrightnessController.Open(identity);
        Console.WriteLine($"  DDC brightness={ddc?.ReadBrightness().ToString() ?? "unavailable"}; Matched WMI brightness={wmi?.ReadBrightness().ToString() ?? "unavailable"}");
        if (!NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(monitor.Handle, out var count) || count != 1) continue;
        var handles = new NativeMethods.PHYSICAL_MONITOR[count];
        if (!NativeMethods.GetPhysicalMonitorsFromHMONITOR(monitor.Handle, count, handles)) continue;
        try
        {
            var handle = handles[0].hPhysicalMonitor;
            var capsOk = NativeMethods.GetMonitorCapabilities(handle, out var caps, out var temperatures);
            var tempOk = NativeMethods.GetMonitorColorTemperature(handle, out var temperature);
            Console.WriteLine($"  CapabilitiesRead={capsOk}; Flags=0x{caps:X}; TemperatureModes=0x{temperatures:X}; CurrentTemperatureMode={(tempOk ? temperature : null)}");
            for (uint channel = 0; channel < 3; channel++)
            {
                var gainOk = NativeMethods.GetMonitorRedGreenOrBlueGain(handle, channel, out var min, out var current, out var max);
                Console.WriteLine($"  RGB[{channel}] readable={gainOk}; range={min}..{max}; current={current}");
            }
        }
        finally { NativeMethods.DestroyPhysicalMonitors(count, handles); }
    }
    Console.WriteLine("Read-only probe completed; no brightness or color writes performed.");
    return;
}

var tests = new (string Name, Func<Task> Run)[]
{
    ("Live manual handoff fades from current levels; explicit resume recomputes clock position", ControlCenterChecks.ManualHandoffAsync),
    ("Missed schedule node after sleep computes current position without replay", ControlCenterChecks.MissedNodeAsync),
    ("Language and focus saves preserve manual overrides and do not replay scene rules", ControlCenterChecks.UnrelatedSaveAsync),
    ("Editing an active plan reapplies targets and disabling it clears the source", ControlCenterChecks.PlanEditAsync),
    ("Offline ratio members do not clamp connected displays or block new displays", ControlCenterChecks.OfflineBindingAsync),
    ("All combined settings persist across languages and preserve user names and offline rules", ControlCenterChecks.SettingsRoundTripAsync),
    ("Focus, plan, fullscreen protection, pause and scene selection share the latest baseline", ControlCenterChecks.FocusPlanFullscreenAsync),
    ("Bilingual resources, language persistence and migration", () => { LocalizationChecks.Run(); return Task.CompletedTask; }),
    ("Six-hour daily fade eases across midnight and interpolates native color", DailyPlanChecks.LongFadeAsync),
    ("Daily fade handles instant, single-node, disabled and overlapping schedules", DailyPlanChecks.BoundariesAsync),
    ("72-hour simulated daily plan has bounded allocations", DailyPlanChecks.AllocationAsync),
    ("Color fades write only changed hardware gains and reapply after restore", async () =>
    {
        var device = new FakeColorDevice();
        var controller = new ControlledDisplay(device);
        for (var kelvin = 6500; kelvin >= 6400; kelvin--)
            await controller.ApplyAsync(60, 60, kelvin);
        Check(device.GainWrites is >= 2 and <= 4);
        var writes = device.GainWrites;
        await controller.ApplyAsync(60, 60, null);
        await controller.ApplyAsync(60, 60, 6400);
        Equal(writes + 1, device.GainWrites);
        await controller.CloseAsync();
        Equal("100,100,100", string.Join(',', device.Gains));
    }),
    ("Slow device coalesces 100000 fade requests into the latest target", async () =>
    {
        var device = new FakeDevice { BlockFirstWrite = true };
        var controller = new ControlledDisplay(device);
        var first = controller.ApplyAsync(60, 10, null);
        await device.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100000; i++)
            Check(ReferenceEquals(first, controller.ApplyAsync(60, i % 100, null)));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(allocated <= 1024);
        var last = controller.ApplyAsync(60, 20, null);
        device.Release.Set();
        await last;
        Equal("10,20", string.Join(',', device.Writes));
        await controller.CloseAsync();
        Equal(60, device.Brightness);
    }),
    ("Native buffer layout matches Windows", () =>
    {
        Equal(IntPtr.Size == 8 ? 264 : 260, Marshal.SizeOf<NativeMethods.PHYSICAL_MONITOR>());
        Equal(IntPtr.Size, Marshal.OffsetOf<NativeMethods.PHYSICAL_MONITOR>("szPhysicalMonitorDescription").ToInt32());
        Equal(840, Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>());
        return Task.CompletedTask;
    }),
    ("WMI matching cannot select another monitor", () =>
    {
        var identity = DisplayIdentity.FromInterfacePath(@"\\?\DISPLAY#BOE1234#5&ABC&0&UID1#{guid}");
        Equal(@"DISPLAY\BOE1234\5&ABC&0&UID1", identity!);
        Check(DisplayIdentity.MatchesWmi(identity!, identity + "_0"));
        Check(!DisplayIdentity.MatchesWmi(identity!, identity + "2_0"));
        Check(!DisplayIdentity.MatchesWmi(identity!, @"DISPLAY\AOC1234\5&ABC&0&UID1_0"));
        Check(DisplayIdentity.FromInterfacePath("unknown") == null);
        return Task.CompletedTask;
    }),
    ("Fullscreen arrival wakes a dimmed secondary and departure starts a fresh delay", async () =>
    {
        var settings = new ControlSettings { FocusEnabled = true, FocusDelaySeconds = 3 };
        var focus = new FocusDimming();
        var device = new FakeDevice();
        var controller = new ControlledDisplay(device);
        async Task Apply(long now, bool fullscreen)
        {
            var dim = focus.Update(settings, false, false, false, fullscreen, now);
            await controller.ApplyAsync(60, dim ? 10 : 60, null);
        }
        await Apply(0, false);
        await Apply(3000, false);
        Equal(10, device.Brightness);
        await Apply(3050, true); // Window moved by keyboard; pointer remains on the primary.
        Equal(60, device.Brightness);
        await Apply(100000, true); // No foreground-window dependency.
        Equal(60, device.Brightness);
        await Apply(100050, false);
        await Apply(103049, false);
        Equal(60, device.Brightness);
        await Apply(103050, false);
        Equal(10, device.Brightness);
        await controller.CloseAsync();
        Equal("10,60,10,60", string.Join(',', device.Writes));
    }),
    ("Fullscreen override respects its switch, pause, primary and pointer activity", () =>
    {
        var settings = new ControlSettings { FocusEnabled = true, FocusDelaySeconds = 0,
            KeepSecondaryAwakeOnFullscreen = false };
        var focus = new FocusDimming();
        Check(focus.Update(settings, false, false, false, true, 0));
        settings.KeepSecondaryAwakeOnFullscreen = true;
        Check(!focus.Update(settings, false, false, false, true, 50));
        Check(!focus.Update(settings, false, true, false, false, 100));
        Check(!focus.Update(settings, true, false, false, false, 150));
        Check(!focus.Update(settings, false, false, true, false, 200));
        settings.FocusEnabled = false;
        Check(!focus.Update(settings, false, false, false, false, 250));
        return Task.CompletedTask;
    }),
    ("Fullscreen bounds match individual displays at negative and scaled desktop coordinates", () =>
    {
        var screen = new System.Drawing.Rectangle(-2560, -400, 2560, 1440);
        Check(FullscreenWindowDetector.MatchesDisplay(screen, screen));
        Check(FullscreenWindowDetector.MatchesDisplay(new(-2561, -400, 2562, 1440), screen));
        Check(!FullscreenWindowDetector.MatchesDisplay(new(-2560, -400, 2560, 1400), screen));
        Check(!FullscreenWindowDetector.MatchesDisplay(new(-2560, -400, 5120, 1440), screen));
        Check(!FullscreenWindowDetector.MatchesDisplay(new(0, 0, 2560, 1440), screen));
        return Task.CompletedTask;
    }),
    ("Existing settings gain fullscreen protection and preserve a saved opt-out", () => WithSettingsDirectory(directory =>
    {
        File.WriteAllText(Path.Combine(directory, "control-center.json"), "{\"FocusEnabled\":true}");
        var store = new ControlSettingsStore(directory);
        var settings = store.Load();
        Check(settings.FocusEnabled && settings.KeepSecondaryAwakeOnFullscreen);
        settings.KeepSecondaryAwakeOnFullscreen = false;
        store.Save(settings);
        Check(!store.Load().KeepSecondaryAwakeOnFullscreen);
    })),
    ("Native detection follows fullscreen and maximized windows across displays", WindowDetectionChecks.RunAsync),
    ("Scene behavior survives saving without sharing mutable settings or losing offline screens", () => WithSettingsDirectory(directory =>
    {
        var settings = new ControlSettings { Linked = true, LinkWeights = new() { ["main"] = 40, ["secondary"] = 60 },
            FocusEnabled = true, ControlledDisplayIds = new() { "secondary", "offline" } };
        var scene = new Scene { Behavior = SceneBehavior.Capture(settings) };
        settings.Scenes.Add(scene);
        settings.LinkWeights["main"] = 20;
        settings.ControlledDisplayIds.Clear();
        var store = new ControlSettingsStore(directory);
        store.Save(settings);
        settings = store.Load();
        var behavior = settings.Scenes.Single().Behavior!;
        Check(behavior.ApplyTo(settings));
        Equal(50, RatioBinding.Calculate(settings.LinkWeights, "main", 50)["main"]);
        Equal(75, RatioBinding.Calculate(settings.LinkWeights, "main", 50)["secondary"]);
        Equal("secondary,offline", string.Join(',', settings.ControlledDisplayIds!));
        Check(!behavior.ApplyTo(settings));
        settings.LinkWeights["main"] = 10;
        settings.ControlledDisplayIds!.Clear();
        Equal(40.0, behavior.LinkWeights["main"]);
        Equal(2, behavior.ControlledDisplayIds!.Count);
        new SceneBehavior { ControlledDisplayIds = new() }.ApplyTo(settings);
        Check(!settings.Linked && !settings.FocusEnabled && settings.ControlledDisplayIds!.Count == 0);
        store.Save(settings);
        Equal(0, store.Load().ControlledDisplayIds!.Count);
    })),
    ("Existing scenes inherit current behavior without rewriting the original configuration", () => WithSettingsDirectory(directory =>
    {
        var path = Path.Combine(directory, "control-center.json");
        const string original = "{\"Linked\":true,\"LinkWeights\":{\"a\":40,\"b\":60},\"FocusEnabled\":true,\"Scenes\":[{\"Name\":\"旧场景\"}]}";
        File.WriteAllText(path, original);
        var store = new ControlSettingsStore(directory);
        var settings = store.Load();
        var behavior = settings.Scenes.Single().Behavior!;
        Check(behavior.Linked && behavior.FocusEnabled && behavior.ControlledDisplayIds == null);
        Equal(original, File.ReadAllText(path));
        settings.LinkWeights["a"] = 20;
        Equal(40.0, behavior.LinkWeights["a"]);
        store.Save(settings);
        Equal(40.0, store.Load().Scenes.Single().Behavior!.LinkWeights["a"]);
    })),
    ("Scene bindings reject unusable ratios before saving", () =>
    {
        foreach (var weights in new Dictionary<string, double>[] { new(), new() { ["a"] = 1 },
            new() { ["a"] = 0, ["b"] = 1 }, new() { ["a"] = double.NaN, ["b"] = 1 } })
        {
            var settings = new ControlSettings { Scenes = new() { new Scene { Behavior = new() { Linked = true, LinkWeights = weights } } } };
            var rejected = false;
            try { DailyPlan.Validate(settings); } catch (ArgumentException) { rejected = true; }
            Check(rejected);
        }
        return Task.CompletedTask;
    }),
    ("Schedule uses the arriving scene rules immediately while brightness fades across midnight", () =>
    {
        var evening = new Scene { Id = "evening", Targets = new() { ["a"] = new(80, 6500) }, Behavior = new() { FocusEnabled = true } };
        var night = new Scene { Id = "night", Targets = new() { ["a"] = new(20, 2700) },
            Behavior = new() { Linked = true, LinkWeights = new() { ["a"] = 1, ["offline"] = 2 }, ControlledDisplayIds = new() } };
        var settings = new ControlSettings { ScheduleEnabled = true, Scenes = new() { evening, night },
            Schedule = new() { new(720, evening.Id, 0), new(0, night.Id, 60) } };
        DailyPlan.Validate(settings);
        var midnight = new DateTime(2026, 11, 1);
        var start = DailyPlan.Evaluate(settings, midnight)!.Value;
        Equal("night", start.Scene.Id);
        Check(start.TryGetTarget("a", out var brightness, out _));
        Equal(80, brightness);
        start.Scene.Behavior!.ApplyTo(settings);
        Check(settings.Linked && !settings.FocusEnabled && settings.ControlledDisplayIds!.Count == 0);
        var middle = DailyPlan.Evaluate(settings, midnight.AddMinutes(30))!.Value;
        Check(middle.TryGetTarget("a", out brightness, out var kelvin));
        Equal(50, brightness);
        Equal(4600, kelvin);
        Equal(midnight.AddHours(12), middle.NextAt);
        Equal("evening", DailyPlan.Evaluate(settings, midnight.AddHours(12))!.Value.Scene.Id);
        return Task.CompletedTask;
    }),
    ("Focus selection excludes primary and unselected screens and restarts the delay after a rule change", () =>
    {
        var settings = new ControlSettings { FocusEnabled = true, FocusDelaySeconds = 3, ControlledDisplayIds = new() { "secondary", "main" } };
        var focus = new FocusDimming();
        Check(!focus.Update(settings, false, false, false, false, 0, "secondary"));
        Check(focus.Update(settings, false, false, false, false, 3000, "secondary"));
        Check(!focus.Update(settings, false, false, false, false, 3050, "other"));
        Check(!focus.Update(settings, false, true, false, false, 3100, "main"));
        settings.ControlledDisplayIds.Clear();
        Check(!focus.Update(settings, false, false, false, false, 3200, "secondary"));
        settings.ControlledDisplayIds = null;
        Check(!focus.Update(settings, false, false, false, false, 3300, "secondary"));
        Check(focus.Update(settings, false, false, false, false, 6300, "secondary"));
        return Task.CompletedTask;
    }),
    ("Center exit restores a raised baseline even when effective brightness stayed unchanged", async () =>
    {
        var device = new FakeDevice { Brightness = 10 };
        var controller = new ControlledDisplay(device);
        await controller.ApplyAsync(10, 10, null);
        await controller.ApplyAsync(60, 10, null);
        await controller.CloseAsync();
        Equal(60, device.Brightness);
        Equal("60", string.Join(',', device.Writes));
        Check(device.Disposed);
    }),
    ("Center exit leaves a new normal brightness untouched", async () =>
    {
        var device = new FakeDevice();
        var controller = new ControlledDisplay(device);
        await controller.ApplyAsync(60, 10, null);
        await controller.ApplyAsync(10, 10, null);
        await controller.CloseAsync();
        Equal("10", string.Join(',', device.Writes));
    }),
    ("Reopened color device applies retained warmth after delayed capability recovery", async () =>
    {
        var first = new FakeColorDevice();
        var controller = new ControlledDisplay(first);
        await controller.ApplyAsync(60, 60, 3200);
        await controller.CloseAsync();
        Equal("100,100,100", string.Join(',', first.Gains));

        var reopened = new FakeColorDevice { FailFirstRead = true };
        var recovered = new ControlledDisplay(reopened);
        try
        {
            Check(!recovered.SupportsColor);
            await recovered.ApplyAsync(60, 60, 3200);
            await reopened.Applied.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Equal("100,76,44", string.Join(',', reopened.Gains));
            Check(recovered.SupportsColor);
        }
        finally { await recovered.CloseAsync(); }
        Equal("100,100,100", string.Join(',', reopened.Gains));
        Check(reopened.Disposed);
    }),
    ("Legacy migration preserves compatible settings and runs only once", () => WithSettingsDirectory(directory =>
    {
        var path = Path.Combine(directory, "settings.json");
        const string original = "{\"LaunchOnStartup\":true,\"DelaySeconds\":42,\"HardwareDimLevel\":17,\"DimmingMode\":\"OverlayOnly\"}";
        File.WriteAllText(path, original);
        var store = new ControlSettingsStore(directory);
        var settings = store.Load();
        Check(settings.LaunchOnStartup);
        Equal(42, settings.FocusDelaySeconds);
        Equal(17, settings.FocusBrightness);
        Check(!settings.FocusEnabled);
        Equal(original, File.ReadAllText(path));
        Equal(original, File.ReadAllText(path + ".pre-center.bak"));
        settings.LaunchOnStartup = false;
        settings.FocusDelaySeconds = 90;
        store.Save(settings);
        var reloaded = store.Load();
        Check(!reloaded.LaunchOnStartup);
        Equal(90, reloaded.FocusDelaySeconds);
        Equal(original, File.ReadAllText(path + ".pre-center.bak"));
    })),
    ("Invalid legacy settings are preserved without creating replacement settings", () => WithSettingsDirectory(directory =>
    {
        var path = Path.Combine(directory, "settings.json");
        File.WriteAllText(path, "broken-json");
        try { new ControlSettingsStore(directory).Load(); throw new Exception("Expected migration error."); }
        catch (System.Text.Json.JsonException) { }
        Equal("broken-json", File.ReadAllText(path));
        Check(!File.Exists(Path.Combine(directory, "control-center.json")));
    })),
    ("Closing untouched device never changes brightness", async () =>
    {
        var device = new FakeDevice();
        var session = new BrightnessSession(device, _ => { });
        await session.CloseAsync();
        Equal(0, device.Writes.Count);
        Check(device.Disposed);
    }),
    ("Restore uses each focus cycle's actual baseline", async () =>
    {
        var device = new FakeDevice();
        var session = new BrightnessSession(device, _ => { });
        await session.DimAsync(10);
        await session.RestoreAsync();
        Equal(60, device.Brightness);
        device.Brightness = 35;
        await session.DimAsync(5);
        await session.RestoreAsync();
        Equal(35, device.Brightness);
        await session.CloseAsync();
        Equal("10,60,5,35", string.Join(',', device.Writes));
    }),
    ("Restore cannot overtake a delayed dim", async () =>
    {
        var device = new FakeDevice { BlockFirstWrite = true };
        var session = new BrightnessSession(device, _ => { }, 3000);
        var dim = session.DimAsync(10);
        await device.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        _ = session.DimAsync(20);
        var restore = session.RestoreAsync();
        Check(!restore.IsCompleted);
        device.Release.Set();
        await Task.WhenAll(dim, restore);
        Equal("10,60", string.Join(',', device.Writes));
        await session.CloseAsync();
    }),
    ("Timeout restores late writes before disposing", async () =>
    {
        var device = new FakeDevice { BlockFirstWrite = true };
        var timedOut = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new BrightnessSession(device, error => { if (error != null) timedOut.TrySetResult(); }, 30);
        _ = session.DimAsync(10);
        await device.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await timedOut.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Check(!session.CanDim);
        var close = session.CloseAsync();
        Check(!close.IsCompleted && !device.Disposed);
        device.Release.Set();
        await close;
        Equal("10,60", string.Join(',', device.Writes));
        Check(device.Disposed);
        await session.DimAsync(5);
        Equal(2, device.Writes.Count);
    }),
    ("Restore failure is reported while handles are released", async () =>
    {
        var device = new FakeDevice { FailRestore = true };
        var session = new BrightnessSession(device, _ => { });
        await session.DimAsync(10);
        try { await session.CloseAsync(); throw new Exception("Expected restore error."); }
        catch (InvalidOperationException) { }
        Check(device.Disposed);
    })
};

foreach (var test in tests)
{
    await test.Run().WaitAsync(TimeSpan.FromSeconds(8));
    Console.WriteLine($"PASS {test.Name}");
}
Console.WriteLine($"{tests.Length} checks passed; no real monitor writes performed.");

static void Check(bool value) { if (!value) throw new Exception("Assertion failed."); }
static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"Expected {expected}, got {actual}.");
}

static Task WithSettingsDirectory(Action<string> run)
{
    var tempRoot = Path.GetFullPath(Path.GetTempPath());
    var directory = Path.Combine(tempRoot, "MonitorsFocus-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try { run(directory); }
    finally
    {
        if (Path.GetDirectoryName(Path.GetFullPath(directory)) != tempRoot.TrimEnd(Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Unexpected test directory.");
        Directory.Delete(directory, recursive: true);
    }
    return Task.CompletedTask;
}

class FakeDevice : IBrightnessDevice
{
    public int Brightness = 60;
    public bool BlockFirstWrite;
    public bool FailRestore;
    public bool Disposed;
    public readonly ConcurrentQueue<int> Writes = new();
    public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public readonly ManualResetEventSlim Release = new();

    public int ReadBrightness()
    {
        if (Disposed) throw new Exception("Read after disposal.");
        return Brightness;
    }

    public void SetBrightness(int value)
    {
        if (Disposed) throw new Exception("Write after disposal.");
        if (BlockFirstWrite && Writes.IsEmpty)
        {
            Entered.TrySetResult();
            if (!Release.Wait(TimeSpan.FromSeconds(4))) throw new TimeoutException("Test did not release fake device.");
        }
        if (Disposed) throw new Exception("Disposed during native write.");
        if (FailRestore && value == 60) throw new InvalidOperationException("Restore rejected.");
        Writes.Enqueue(value);
        Brightness = value;
    }

    public void Dispose() { Disposed = true; Release.Dispose(); }
}

sealed class FakeColorDevice : FakeDevice, IColorDevice
{
    public int GainWrites;
    public bool FailFirstRead;
    private int _reads;
    public uint[] Gains = { 100, 100, 100 };
    public readonly TaskCompletionSource Applied = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public uint[]? ReadGains() => ++_reads == 1 && FailFirstRead ? null : Gains.ToArray();
    public void SetGains(uint[] values) { GainWrites++; Gains = values.ToArray(); Applied.TrySetResult(); }
    public void RestoreGains(uint[] values) => Gains = values.ToArray();
}
