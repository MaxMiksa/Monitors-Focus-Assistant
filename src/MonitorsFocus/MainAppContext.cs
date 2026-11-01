using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using MonitorsFocus.Brightness;
using MonitorsFocus.Core;
using MonitorsFocus.Monitoring;
using MonitorsFocus.Overlay;
using MonitorsFocus.Settings;
using MonitorsFocus.UI;

namespace MonitorsFocus;

internal sealed class MainAppContext : ApplicationContext
{
    private readonly SettingsStore _settingsStore;
    private AppSettings _settings;
    private readonly MonitorManager _monitorManager;
    private readonly OverlayManager _overlayManager;
    private readonly HardwareDimmer _hardwareDimmer;
    private readonly MouseTracker _mouseTracker;
    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _pauseMenuItem;
    private readonly SynchronizationContext _uiContext;
    private bool _paused;
    private bool _exiting;
    private int _hardwareRefresh;
    private string T(string key) => Localization.Text(key, _settings.Language.Preference());

    public MainAppContext()
    {
        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Monitors-Focus",
            "settings.json");

        _settingsStore = new SettingsStore(settingsPath);
        _settings = _settingsStore.Load();

        _uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        _monitorManager = new MonitorManager();
        _overlayManager = new OverlayManager(_monitorManager, _settings);
        _hardwareDimmer = new HardwareDimmer();
        _monitorManager.MonitorsChanged += OnMonitorsChanged;

        _mouseTracker = new MouseTracker(50);
        _mouseTracker.Tick += OnMouseTick;
        _mouseTracker.Start();

        _pauseMenuItem = new ToolStripMenuItem();
        _pauseMenuItem.Click += (_, _) => TogglePaused();
        UpdatePauseMenuText();

        var settingsItem = new ToolStripMenuItem(T("设置…"));
        settingsItem.Click += (_, _) => OpenSettings();
        var exitItem = new ToolStripMenuItem(T("退出"));
        exitItem.Click += (_, _) => ExitThread();

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(settingsItem);
        contextMenu.Items.Add(_pauseMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);
        contextMenu.Opening += (_, _) => { settingsItem.Text = T("设置…"); exitItem.Text = T("退出"); UpdatePauseMenuText(); };

        _trayIcon = new NotifyIcon
        {
            Text = "Monitors Focus",
            Icon = AppIcons.Tray(),
            Visible = true,
            ContextMenuStrip = contextMenu
        };
        _trayIcon.MouseUp += OnTrayMouseUp;
        _ = RefreshHardwareAsync();
    }

    public void TogglePaused()
    {
        SetPaused(!_paused);
    }

    public void SetPaused(bool paused)
    {
        if (_exiting) return;
        _paused = paused;
        UpdatePauseMenuText();
        if (_paused)
        {
            _overlayManager.HideAll();
            _hardwareDimmer.RestoreAll();
        }
        else
        {
            ResetMaskTimers();
        }
    }

    private void OnTrayMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            OpenSettings();
        }
    }

    private void OnMouseTick(Point position, Screen screen)
    {
        if (_paused || _exiting)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var entry in _overlayManager.Entries.Values)
        {
            var isActiveMonitor = entry.Monitor.Id == screen.DeviceName;
            if (isActiveMonitor)
            {
                if (entry.IsMasked)
                {
                    RestoreHardwareBrightness(entry);
                    entry.Form.HideOverlay();
                    entry.IsMasked = false;
                    LogSink.Info($"Unmask (cursor on): {entry.Monitor.Id}");
                }

                entry.NextMaskAt = null;
                continue;
            }

            if (entry.NextMaskAt == null)
            {
                var delay = TimeSpan.FromSeconds(_settings.DelaySeconds);
                entry.NextMaskAt = delay == TimeSpan.Zero ? now : now.Add(delay);
            }

            if (!entry.IsMasked && entry.NextMaskAt <= now)
            {
                ApplyHardwareDim(entry);
                entry.Form.ShowOverlay();
                entry.IsMasked = true;
                LogSink.Info($"Mask applied: {entry.Monitor.Id}");
            }
        }
    }

    private void OnMonitorsChanged(object? sender, EventArgs e)
    {
        _uiContext.Post(_ =>
        {
            if (_exiting) return;
            _overlayManager.Rebuild(_settings);
            _ = RefreshHardwareAsync();
            ResetMaskTimers();
            LogSink.Info("Monitor change detected; overlays and dimmer refreshed.");
        }, null);
    }

    private void ResetMaskTimers()
    {
        foreach (var entry in _overlayManager.Entries.Values)
        {
            entry.NextMaskAt = null;
        }
    }

    private void UpdatePauseMenuText()
    {
        _pauseMenuItem.Text = _paused ? T("恢复") : T("暂停");
    }

    private void OpenSettings()
    {
        if (_exiting) return;
        using var form = new SettingsForm(_settings, _monitorManager.Monitors, _hardwareDimmer.Capabilities);
        if (form.ShowDialog() == DialogResult.OK && form.ResultSettings != null)
        {
            ApplySettings(form.ResultSettings);
        }
    }

    private void ApplySettings(AppSettings newSettings)
    {
        _settings = newSettings;
        try
        {
            _settingsStore.Save(_settings);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                Localization.Format("保存设置失败：{0}", _settings.Language.Preference(), Localization.Message(ex.Message, _settings.Language.Preference())),
                "Monitors Focus",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        _overlayManager.Rebuild(_settings);
        _ = RefreshHardwareAsync();
        ResetMaskTimers();
        LogSink.Info($"Settings applied: mode={_settings.DimmingMode}, hwDim={_settings.EnableDdcCi}, level={_settings.HardwareDimLevel}%, opacity={_settings.OverlayOpacity}%.");
        foreach (var kv in _hardwareDimmer.Capabilities)
        {
            LogSink.Info($"Capability status: {kv.Key} -> {kv.Value.Status} ({kv.Value.Message ?? "n/a"})");
        }

        // 自启条目统一指向控制核心，旧托盘不再写入无参启动项，避免登录时拉起旧界面。
        StartupManager.Apply(_settings.LaunchOnStartup, StartupManager.CenterArguments);

        if (_paused)
        {
            _overlayManager.HideAll();
            _hardwareDimmer.RestoreAll();
        }
    }

    private async Task RefreshHardwareAsync()
    {
        var refresh = ++_hardwareRefresh;
        try
        {
            await _hardwareDimmer.RefreshAsync(_monitorManager.Monitors,
                _settings.HardwareDimDisabledMonitorIds,
                _settings.EnableDdcCi && _settings.DimmingMode != DimmingMode.OverlayOnly);
            if (_exiting || refresh != _hardwareRefresh) return;
            _overlayManager.HideAll();
            ResetMaskTimers();
        }
        catch (Exception ex)
        {
            LogSink.Info($"Hardware refresh failed: {ex.Message}");
            if (!_exiting)
                MessageBox.Show(Localization.Format("硬件刷新失败：{0}", _settings.Language.Preference(), Localization.Message(ex.Message, _settings.Language.Preference())), "Monitors Focus",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    protected override async void ExitThreadCore()
    {
        if (_exiting) return;
        _exiting = true;
        _mouseTracker.Tick -= OnMouseTick;
        _mouseTracker.Dispose();
        _monitorManager.MonitorsChanged -= OnMonitorsChanged;
        _monitorManager.Dispose();
        _overlayManager.Dispose();
        _trayIcon.Text = T("Monitors Focus - 正在恢复显示器");
        try { await _hardwareDimmer.ShutdownAsync(); }
        catch (Exception ex)
        {
            MessageBox.Show(Localization.Format("显示器恢复失败：{0}", _settings.Language.Preference(), Localization.Message(ex.Message, _settings.Language.Preference())), "Monitors Focus",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        base.ExitThreadCore();
    }

    private void ApplyHardwareDim(OverlayEntry entry)
    {
        var allowHardware = _settings.EnableDdcCi && _settings.DimmingMode != DimmingMode.OverlayOnly;
        var preferHardware = _settings.DimmingMode != DimmingMode.OverlayOnly;
        if (!allowHardware)
        {
            LogSink.Info($"Hardware dim skipped (disabled/mode): {entry.Monitor.Id}, mode={_settings.DimmingMode}, hwEnabled={_settings.EnableDdcCi}");
        }
        if (allowHardware && _hardwareDimmer.CanDim(entry.Monitor.Id))
        {
            _hardwareDimmer.Dim(entry.Monitor.Id, _settings.HardwareDimLevel);
            LogSink.Info($"Hardware dim attempt: {entry.Monitor.Id} -> {_settings.HardwareDimLevel}%");
            return;
        }

        if (_settings.DimmingMode == DimmingMode.HardwareOnly && preferHardware)
        {
            LogSink.Info($"Hardware dim not available for {entry.Monitor.Id}; hardware-only mode, overlay still applied.");
        }
        else
        {
            LogSink.Info($"Hardware dim skipped: allow={allowHardware}, canDim={_hardwareDimmer.CanDim(entry.Monitor.Id)}, mode={_settings.DimmingMode}");
        }
    }

    private void RestoreHardwareBrightness(OverlayEntry entry)
    {
        if (_settings.EnableDdcCi)
        {
            _hardwareDimmer.Restore(entry.Monitor.Id);
            LogSink.Info($"Hardware dim restore: {entry.Monitor.Id}");
        }
    }
}
