using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Win32;
using MonitorsFocus.Brightness;
using MonitorsFocus.Core;
using MonitorsFocus.Monitoring;
using MonitorsFocus.Overlay;
using MonitorsFocus.Settings;
using Timer = System.Windows.Forms.Timer;

namespace MonitorsFocus;

internal sealed class ControlCenterContext : ApplicationContext
{
    private sealed class Display
    {
        public required MonitorInfo Monitor;
        public required string Id;
        public required string Name;
        public ControlledDisplay? Controller;
        public int Brightness;
        public int? Kelvin;
        public string? Error;
        public OverlayForm? Overlay;
        public readonly FocusDimming Focus = new();
        public (int, int, int?)? LastRequest;
    }

    private readonly SynchronizationContext _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly MonitorManager _monitors = new();
    private readonly ControlSettingsStore _store = new();
    private readonly List<Display> _displays = new();
    private readonly NotifyIcon _tray;
    private readonly Timer _timer = new() { Interval = 50 };
    private readonly SemaphoreSlim _scanGate = new(1);
    private ControlSettings _settings;
    private bool _paused;
    private bool _scanning;
    private bool _closing;
    private DateTime? _manualUntil;
    private DateTime? _activePlanAt;
    private long _nextPlanAt;
    private string _source = "手动调节";
    private string? _error;
    private string? _planError;

    public ControlCenterContext(bool openPanel)
    {
        try { _settings = _store.Load(); }
        catch (Exception ex) { _settings = new(); _error = $"原配置未覆盖：{ex.Message}"; }
        // 安装位置变更或条目被外部改动后，登录自启在这里校正，不要求用户重开一次开关。
        StartupManager.Ensure(_settings.LaunchOnStartup);
        var menu = new ContextMenuStrip();
        menu.Items.Add("打开控制面板", null, (_, _) => OpenPanel());
        menu.Items.Add("暂停 / 恢复专注", null, (_, _) => { _paused = !_paused; Tick(); });
        menu.Items.Add("恢复原色", null, (_, _) => { foreach (var d in _displays) d.Kelvin = null; HoldManual(); Tick(); });
        menu.Items.Add("退出", null, (_, _) => ExitThread());
        _tray = new NotifyIcon { Text = "Monitors Focus", Icon = AppIcons.Tray(), Visible = true, ContextMenuStrip = menu };
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) OpenPanel(); };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        _monitors.MonitorsChanged += MonitorsChanged;
        SystemEvents.PowerModeChanged += PowerChanged;
        SystemEvents.UserPreferenceChanged += ThemeChanged;
        _ = ServeAsync();
        _ = ScanAsync();
        if (openPanel) OpenPanel();
    }

    private void MonitorsChanged(object? sender, EventArgs e) => _ui.Post(_ => { if (!_closing) _ = ScanAsync(); }, null);
    private void ThemeChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General) _ui.Post(_ => { if (!_closing) { var old = _tray.Icon; _tray.Icon = AppIcons.Tray(); old?.Dispose(); } }, null);
    }
    private void PowerChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) _ui.Post(_ => { if (!_closing) _ = ScanAsync(); }, null);
    }

    private async Task ScanAsync()
    {
        if (_closing) return;
        _scanning = true;
        await _scanGate.WaitAsync();
        try
        {
            if (_closing) return;
            await CloseDisplaysAsync();
            var monitors = _monitors.Monitors.ToArray();
            foreach (var monitor in monitors)
            {
                var result = await Task.Run(() =>
                {
                    var identity = DisplayIdentity.FindInstance(monitor.Id);
                    IBrightnessDevice? device = null;
                    ControlledDisplay? controller = null;
                    string? error = null;
                    try
                    {
                        for (var attempt = 0; attempt < 2 && device == null; attempt++)
                        {
                            device = DdcBrightnessDevice.Open(monitor);
                            if (device == null && attempt == 0) Thread.Sleep(200);
                        }
                        device ??= WmiBrightnessController.Open(identity);
                        if (device != null) controller = new ControlledDisplay(device);
                    }
                    catch (Exception ex) { device?.Dispose(); error = ex.Message; }
                    return new Display
                    {
                        Monitor = monitor, Id = identity ?? monitor.Id,
                        Name = identity?.Contains("AOC", StringComparison.OrdinalIgnoreCase) == true ? "AOC U27G4" :
                            identity?.Contains("BOE", StringComparison.OrdinalIgnoreCase) == true ? "笔记本屏幕" : monitor.Id,
                        Controller = controller, Brightness = controller?.InitialBrightness ?? 0,
                        Error = error ?? (controller == null ? "硬件亮度不可用" : null)
                    };
                });
                result.Overlay = new OverlayForm(monitor.Bounds, 100);
                _displays.Add(result);
            }
            if (_settings.Scenes.Count == 0)
            {
                foreach (var (name, factor, kelvin) in new[] { ("日常", 1.0, 6500), ("夜间", 0.5, 3200), ("专注", 0.7, 4500) })
                    _settings.Scenes.Add(new Scene { Name = name, Behavior = SceneBehavior.Capture(_settings), Targets = _displays.ToDictionary(d => d.Id,
                        d => new DisplayTarget((int)Math.Round(d.Brightness * factor), d.Controller?.SupportsColor == true ? kelvin : null)) });
            }
            _nextPlanAt = 0;
        }
        catch (Exception ex) { _error = $"检测失败：{ex.Message}"; }
        finally { _scanning = false; _scanGate.Release(); }
    }

    private void Tick()
    {
        if (_closing || _scanning) return;
        var now = Environment.TickCount64;
        if (now >= _nextPlanAt)
        {
            _nextPlanAt = now + 1000;
            var plan = DailyPlan.Evaluate(_settings, DateTime.Now);
            if (plan is { } activePlan && (!_manualUntil.HasValue || DateTime.Now >= _manualUntil))
            {
                _manualUntil = null;
                try
                {
                    if (_activePlanAt != activePlan.StartsAt)
                    {
                        ApplySceneBehavior(activePlan.Scene);
                        _activePlanAt = activePlan.StartsAt;
                    }
                    foreach (var d in _displays)
                        if (activePlan.TryGetTarget(d.Id, out var brightness, out var kelvin))
                        {
                            d.Brightness = brightness;
                            d.Kelvin = d.Controller?.SupportsColor == true ? kelvin : null;
                        }
                    _source = $"时间计划 · {activePlan.Scene.Name}";
                    _planError = null;
                }
                catch (Exception ex) { _planError = $"时间计划未应用：{ex.Message}"; }
            }
        }
        var cursor = _settings.FocusEnabled && !_paused ? Cursor.Position : Point.Empty;
        foreach (var d in _displays)
        {
            var dim = d.Focus.Update(_settings, _paused, d.Monitor.IsPrimary, d.Monitor.Bounds.Contains(cursor), now, d.Id);
            if (dim) d.Overlay?.ShowOverlay(); else d.Overlay?.HideOverlay();
            var effective = dim ? Math.Min(d.Brightness, _settings.FocusBrightness) : d.Brightness;
            var target = (d.Brightness, effective, d.Kelvin);
            if (d.LastRequest == target) continue;
            d.LastRequest = target;
            if (d.Controller != null) _ = d.Controller.ApplyAsync(d.Brightness, effective, d.Kelvin);
        }
        _timer.Interval = _settings.FocusEnabled && !_paused ? 50 : 1000;
    }

    private void ApplyTargets(Dictionary<string, DisplayTarget> targets)
    {
        foreach (var d in _displays)
            if (targets.TryGetValue(d.Id, out var target))
            {
                d.Brightness = target.Brightness;
                d.Kelvin = d.Controller?.SupportsColor == true ? QuantizeKelvin(target.Kelvin) : null;
            }
    }

    private static int? QuantizeKelvin(int? value) => value.HasValue ? (int)Math.Round(value.Value / 100.0) * 100 : null;

    private void ApplySceneBehavior(Scene scene)
    {
        var settings = CloneSettings();
        if (scene.Behavior?.ApplyTo(settings) == true)
        {
            _store.Save(settings);
            _settings = settings;
        }
        foreach (var display in _displays) display.Focus.Reset();
    }

    private void HoldManual()
    {
        _manualUntil = DailyPlan.Evaluate(_settings, DateTime.Now)?.NextAt;
        _planError = null;
        _source = _manualUntil.HasValue ? $"手动 · 保持至 {_manualUntil:HH:mm}" : "手动调节";
    }

    private ControlSnapshot Handle(ControlRequest request)
    {
        try
        {
            if (_closing) throw new InvalidOperationException("正在退出。");
            if (_scanning && request.Action is not "snapshot") throw new InvalidOperationException("正在检测显示器，请稍候。");
            switch (request.Action)
            {
                case "brightness":
                    var display = _displays.Single(d => d.Id == request.Id);
                    if (display.Controller == null) throw new InvalidOperationException("此显示器不支持亮度调节。");
                    var value = Math.Clamp(request.Value ?? display.Brightness, 0, 100);
                    var values = _settings.Linked ? RatioBinding.Calculate(_settings.LinkWeights, display.Id, value)
                        : new Dictionary<string, int> { [display.Id] = value };
                    foreach (var d in _displays)
                        if (values.TryGetValue(d.Id, out var brightness)) { d.Brightness = brightness; d.Focus.Reset(); }
                    HoldManual();
                    break;
                case "temperature":
                    foreach (var d in _displays.Where(d => d.Controller?.SupportsColor == true))
                        d.Kelvin = QuantizeKelvin(Math.Clamp(request.Value ?? 6500, 2700, 6500));
                    HoldManual();
                    break;
                case "restore-color":
                    foreach (var d in _displays) d.Kelvin = null;
                    HoldManual();
                    break;
                case "link":
                    var enabled = request.Value == 1;
                    var weights = _displays.Where(d => d.Controller != null).ToDictionary(d => d.Id, d => (double)d.Brightness);
                    if (enabled && (weights.Count < 2 || weights.Values.Any(x => x <= 0)))
                        throw new ArgumentException("请先为两块可调屏幕设置非零亮度。");
                    var linkedSettings = CloneSettings();
                    linkedSettings.Linked = enabled;
                    if (enabled) linkedSettings.LinkWeights = weights;
                    _store.Save(linkedSettings);
                    _settings = linkedSettings;
                    break;
                case "scene":
                    var scene = _settings.Scenes.Single(s => s.Id == request.Id);
                    ApplySceneBehavior(scene);
                    ApplyTargets(scene.Targets);
                    HoldManual();
                    break;
                case "pause": _paused = !_paused; break;
                case "resume-plan": _manualUntil = null; _activePlanAt = null; _nextPlanAt = 0; break;
                case "rescan": _ = ScanAsync(); break;
                case "save":
                    var settings = request.Settings ?? throw new ArgumentException("缺少设置。");
                    if (settings.Revision != _settings.Revision) throw new InvalidOperationException("设置已在其他操作中更新。请重新载入设置后再保存。");
                    DailyPlan.Validate(settings);
                    StartupManager.Apply(settings.LaunchOnStartup, StartupManager.CenterArguments, reportFailure: true);
                    try { _store.Save(settings); }
                    catch
                    {
                        StartupManager.Apply(_settings.LaunchOnStartup, StartupManager.CenterArguments);
                        throw;
                    }
                    _settings = settings;
                    _planError = null;
                    _activePlanAt = null;
                    _nextPlanAt = 0;
                    break;
                case "snapshot": break;
                case "exit": _ui.Post(_ => ExitThread(), null); break;
                default: throw new ArgumentException("未知操作。");
            }
            Tick();
            return Snapshot();
        }
        catch (Exception ex) { return Snapshot(ex.Message); }
    }

    private ControlSettings CloneSettings() => JsonSerializer.Deserialize<ControlSettings>(JsonSerializer.Serialize(_settings))!;

    private ControlSnapshot Snapshot(string? error = null) => new(_displays.Select(d => new DisplayState(d.Id, d.Name,
        d.Brightness, d.Controller != null, d.Controller?.SupportsColor == true, d.Kelvin,
        d.Controller?.Error ?? d.Error, d.Monitor.IsPrimary)).ToList(), CloneSettings(), _paused,
        _scanning ? "正在检测显示器…" : _source, error, _planError ?? _error);

    private async Task ServeAsync()
    {
        while (!_lifetime.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(ControlProtocol.PipeName, PipeDirection.InOut, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(_lifetime.Token).ConfigureAwait(false);
                using var reader = new StreamReader(pipe, leaveOpen: true);
                using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
                var line = await reader.ReadLineAsync(_lifetime.Token).ConfigureAwait(false);
                if (line == null || line.Length > 65536) continue;
                var request = JsonSerializer.Deserialize<ControlRequest>(line) ?? new ControlRequest("snapshot");
                var completion = new TaskCompletionSource<ControlSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
                _ui.Post(_ => completion.TrySetResult(Handle(request)), null);
                var snapshot = await completion.Task.WaitAsync(_lifetime.Token).ConfigureAwait(false);
                await writer.WriteLineAsync(JsonSerializer.Serialize(snapshot)).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { LogSink.Info($"Control connection: {ex.Message}"); }
        }
    }

    private static void OpenPanel()
    {
        var desktop = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "desktop", "MonitorsFocus.WebHost.exe"));
        if (!File.Exists(desktop))
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "MonitorsFocus.sln"))) root = root.Parent;
            if (root != null) desktop = Path.Combine(root.FullName, "src", "MonitorsFocus.WebHost", "bin", "Release",
                "net10.0-windows", "MonitorsFocus.WebHost.exe");
        }
        if (File.Exists(desktop)) Process.Start(new ProcessStartInfo(desktop) { UseShellExecute = true });
        else MessageBox.Show("请先构建控制面板，或使用完整发布包。", "Monitors Focus");
    }

    private async Task CloseDisplaysAsync()
    {
        var displays = _displays.ToArray();
        _displays.Clear();
        foreach (var d in displays) d.Overlay?.Dispose();
        await Task.WhenAll(displays.Where(d => d.Controller != null).Select(d => d.Controller!.CloseAsync()));
    }

    protected override async void ExitThreadCore()
    {
        if (_closing) return;
        _closing = true;
        _lifetime.Cancel();
        _timer.Dispose();
        SystemEvents.PowerModeChanged -= PowerChanged;
        SystemEvents.UserPreferenceChanged -= ThemeChanged;
        _monitors.MonitorsChanged -= MonitorsChanged;
        _monitors.Dispose();
        _tray.Text = "Monitors Focus - restoring displays";
        await _scanGate.WaitAsync();
        try { await CloseDisplaysAsync(); }
        catch (Exception ex) { MessageBox.Show($"显示器恢复失败：{ex.Message}", "Monitors Focus"); }
        finally { _scanGate.Release(); _tray.Dispose(); base.ExitThreadCore(); }
    }
}
