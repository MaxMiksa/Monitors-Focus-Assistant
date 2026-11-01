using System.Runtime.InteropServices;
using System.Text;

namespace MonitorsFocus.Monitoring;

internal static class FullscreenWindowDetector
{
    public static HashSet<string> FindDisplays(IReadOnlyList<MonitorInfo> monitors)
    {
        var result = new HashSet<string>();
        // Enumerate all windows: video on the secondary must stay lit after focus returns to the primary.
        NativeMethods.EnumWindows((window, _) =>
        {
            if (FindDisplay(window, monitors) is { } id) result.Add(id);
            return true;
        }, IntPtr.Zero);
        return result;
    }

    internal static string? FindDisplay(IntPtr window, IReadOnlyList<MonitorInfo> monitors)
    {
        if (!NativeMethods.IsWindowVisible(window) || NativeMethods.IsIconic(window)) return null;
        NativeMethods.GetWindowThreadProcessId(window, out var processId);
        if (processId == Environment.ProcessId) return null; // Our full-screen dimming overlays.
        var className = new StringBuilder(256);
        NativeMethods.GetClassName(window, className, className.Capacity);
        if (className.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return null;
        if (NativeMethods.DwmGetWindowAttribute(window, 14, out int cloaked, sizeof(int)) != 0 || cloaked != 0) return null;
        if (NativeMethods.IsZoomed(window))
        {
            var handle = NativeMethods.MonitorFromWindow(window, 0);
            return monitors.FirstOrDefault(m => m.Handle == handle)?.Id;
        }
        if (NativeMethods.DwmGetWindowAttribute(window, 9, out NativeMethods.RECT rect,
                Marshal.SizeOf<NativeMethods.RECT>()) != 0 && !NativeMethods.GetWindowRect(window, out rect)) return null;
        var bounds = rect.ToRectangle();
        return monitors.FirstOrDefault(m => MatchesDisplay(bounds, m.Bounds))?.Id;
    }

    internal static bool MatchesDisplay(Rectangle window, Rectangle display)
    {
        const int tolerance = 2;
        return Math.Abs((long)window.Left - display.Left) <= tolerance &&
            Math.Abs((long)window.Top - display.Top) <= tolerance &&
            Math.Abs((long)window.Right - display.Right) <= tolerance &&
            Math.Abs((long)window.Bottom - display.Bottom) <= tolerance;
    }
}
