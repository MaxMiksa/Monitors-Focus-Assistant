using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
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
        }
        else
        {
            ResetMaskTimers();
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
                    entry.Form.HideOverlay();
                    entry.IsMasked = false;
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
                entry.Form.ShowOverlay();
                entry.IsMasked = true;
            }
        }
    }

    private void OnMonitorsChanged(object? sender, EventArgs e)
    {
        _uiContext.Post(_ =>
        {
            _overlayManager.Rebuild(_settings);
            ResetMaskTimers();
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
        using var form = new SettingsForm(_settings, _monitorManager.Monitors);
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
        ResetMaskTimers();

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
        }
    }

    protected override void ExitThreadCore()
    {
        _mouseTracker.Tick -= OnMouseTick;
        _mouseTracker.Dispose();
        _monitorManager.MonitorsChanged -= OnMonitorsChanged;
        _monitorManager.Dispose();
        _overlayManager.Dispose();
        _hotkeyManager.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        base.ExitThreadCore();
    }
}
