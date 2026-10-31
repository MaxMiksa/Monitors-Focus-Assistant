using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;
using MonitorsFocus;

namespace MonitorsFocus.Monitoring;

internal sealed class MonitorManager : IDisposable
{
    private MonitorInfo[] _monitors = Array.Empty<MonitorInfo>();

    public MonitorManager()
    {
        Refresh();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public IReadOnlyList<MonitorInfo> Monitors => Volatile.Read(ref _monitors);

    public event EventHandler? MonitorsChanged;

    public void Refresh()
    {
        var monitors = new List<MonitorInfo>();
        foreach (var screen in Screen.AllScreens)
        {
            var center = new Point(
                screen.Bounds.Left + screen.Bounds.Width / 2,
                screen.Bounds.Top + screen.Bounds.Height / 2);
            var hMonitor = NativeMethods.MonitorFromPoint(center, NativeMethods.MONITOR_DEFAULTTONEAREST);
            monitors.Add(new MonitorInfo(screen, hMonitor));
        }
        Volatile.Write(ref _monitors, monitors.ToArray());
    }

    public MonitorInfo? FindById(string id)
    {
        return _monitors.FirstOrDefault(monitor => monitor.Id == id);
    }

    public IEnumerable<MonitorInfo> GetControlledMonitors(IReadOnlyCollection<string> controlledIds)
    {
        if (controlledIds.Count == 0)
        {
            return _monitors.Where(monitor => !monitor.IsPrimary);
        }

        return _monitors.Where(monitor => controlledIds.Contains(monitor.Id));
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Refresh();
        MonitorsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }
}
