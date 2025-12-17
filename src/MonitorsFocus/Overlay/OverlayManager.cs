using System;
using System.Collections.Generic;
using MonitorsFocus.Monitoring;
using MonitorsFocus.Settings;

namespace MonitorsFocus.Overlay;

internal sealed class OverlayEntry
{
    public OverlayEntry(MonitorInfo monitor, OverlayForm form)
    {
        Monitor = monitor;
        Form = form;
    }

    public MonitorInfo Monitor { get; }
    public OverlayForm Form { get; }
    public DateTime? NextMaskAt { get; set; }
    public bool IsMasked { get; set; }
}

internal sealed class OverlayManager : IDisposable
{
    private readonly Dictionary<string, OverlayEntry> _entries = new();
    private readonly MonitorManager _monitorManager;
    private AppSettings _settings;

    public OverlayManager(MonitorManager monitorManager, AppSettings settings)
    {
        _monitorManager = monitorManager;
        _settings = settings;
        BuildOverlays();
    }

    public IReadOnlyDictionary<string, OverlayEntry> Entries => _entries;

    public void Rebuild(AppSettings settings)
    {
        _settings = settings;
        BuildOverlays();
    }

    public void ApplyOpacity(int opacityPercent)
    {
        foreach (var entry in _entries.Values)
        {
            entry.Form.ApplyOpacity(opacityPercent);
        }
    }

    public void HideAll()
    {
        foreach (var entry in _entries.Values)
        {
            entry.Form.HideOverlay();
            entry.IsMasked = false;
            entry.NextMaskAt = null;
        }
    }

    private void BuildOverlays()
    {
        foreach (var entry in _entries.Values)
        {
            entry.Form.HideOverlay();
            entry.Form.Close();
            entry.Form.Dispose();
        }
        _entries.Clear();

        foreach (var monitor in _monitorManager.GetControlledMonitors(_settings.ControlledMonitorIds))
        {
            var form = new OverlayForm(monitor.Bounds, _settings.OverlayOpacity);
            _entries[monitor.Id] = new OverlayEntry(monitor, form);
        }
    }

    public void Dispose()
    {
        foreach (var entry in _entries.Values)
        {
            entry.Form.HideOverlay();
            entry.Form.Close();
            entry.Form.Dispose();
        }
        _entries.Clear();
    }
}
