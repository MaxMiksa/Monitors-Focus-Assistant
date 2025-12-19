using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using MonitorsFocus.Brightness;
using MonitorsFocus.Hotkeys;
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
    private readonly HotkeyManager _hotkeyManager;
    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _pauseMenuItem;
    private readonly SynchronizationContext _uiContext;
    private bool _paused;

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
        _hardwareDimmer.Refresh(_monitorManager.Monitors, _settings.HardwareDimDisabledMonitorIds);
        _monitorManager.MonitorsChanged += OnMonitorsChanged;

        _mouseTracker = new MouseTracker(50);
        _mouseTracker.Tick += OnMouseTick;
        _mouseTracker.Start();

        _hotkeyManager = new HotkeyManager();
        _hotkeyManager.HotkeyPressed += TogglePaused;
        _hotkeyManager.Register(_settings.Hotkey);

        _pauseMenuItem = new ToolStripMenuItem();
        _pauseMenuItem.Click += (_, _) => TogglePaused();
        UpdatePauseMenuText();

        var settingsItem = new ToolStripMenuItem("Settings...");
        settingsItem.Click += (_, _) => OpenSettings();
        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitThread();

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(settingsItem);
        contextMenu.Items.Add(_pauseMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        _trayIcon = new NotifyIcon
        {
            Text = "Monitors Focus",
            Icon = SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = contextMenu
        };
        _trayIcon.MouseUp += OnTrayMouseUp;
    }

    public void TogglePaused()
    {
        SetPaused(!_paused);
    }

    public void SetPaused(bool paused)
    {
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
        if (_paused)
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
            _overlayManager.Rebuild(_settings);
            _hardwareDimmer.Refresh(_monitorManager.Monitors, _settings.HardwareDimDisabledMonitorIds);
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
        _pauseMenuItem.Text = _paused ? "Resume" : "Pause";
    }

    private void OpenSettings()
    {
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
                $"Failed to save settings: {ex.Message}",
                "Monitors Focus",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        _overlayManager.Rebuild(_settings);
        _hardwareDimmer.Refresh(_monitorManager.Monitors, _settings.HardwareDimDisabledMonitorIds);
        ResetMaskTimers();
        LogSink.Info($"Settings applied: mode={_settings.DimmingMode}, hwDim={_settings.EnableDdcCi}, level={_settings.HardwareDimLevel}%, opacity={_settings.OverlayOpacity}%.");
        foreach (var kv in _hardwareDimmer.Capabilities)
        {
            LogSink.Info($"Capability status: {kv.Key} -> {kv.Value.Status} ({kv.Value.Message ?? "n/a"})");
        }

        if (!_hotkeyManager.Register(_settings.Hotkey))
        {
            MessageBox.Show(
                "Failed to register the global hotkey. It may be used by another app.",
                "Monitors Focus",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        StartupManager.Apply("MonitorsFocus", _settings.LaunchOnStartup);

        if (_paused)
        {
            _overlayManager.HideAll();
            _hardwareDimmer.RestoreAll();
        }
    }

    protected override void ExitThreadCore()
    {
        _mouseTracker.Tick -= OnMouseTick;
        _mouseTracker.Dispose();
        _monitorManager.MonitorsChanged -= OnMonitorsChanged;
        _monitorManager.Dispose();
        _overlayManager.Dispose();
        _hardwareDimmer.Dispose();
        _hotkeyManager.Dispose();
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
