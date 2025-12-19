using System.Drawing;
using System.Windows.Forms;

namespace MonitorsFocus.Monitoring;

internal sealed class MonitorInfo
{
    public MonitorInfo(Screen screen, IntPtr hMonitor)
    {
        Screen = screen;
        Id = screen.DeviceName;
        Bounds = screen.Bounds;
        IsPrimary = screen.Primary;
        Handle = hMonitor;
    }

    public string Id { get; }
    public Screen Screen { get; }
    public Rectangle Bounds { get; }
    public bool IsPrimary { get; }
    public IntPtr Handle { get; }
}
