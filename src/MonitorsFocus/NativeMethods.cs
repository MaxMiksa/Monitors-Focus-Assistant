using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MonitorsFocus;

internal static class NativeMethods
{
    internal const int WS_EX_LAYERED = 0x00080000;
    internal const int WS_EX_TRANSPARENT = 0x00000020;
    internal const int WS_EX_TOOLWINDOW = 0x00000080;
    internal const int WS_EX_NOACTIVATE = 0x08000000;

    internal const uint SWP_NOSIZE = 0x0001;
    internal const uint SWP_NOMOVE = 0x0002;
    internal const uint SWP_NOACTIVATE = 0x0010;
    internal const uint SWP_SHOWWINDOW = 0x0040;

    internal static readonly IntPtr HWND_TOPMOST = new(-1);

    internal const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    internal delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    internal static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    internal static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    internal static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll")]
    internal static extern bool IsZoomed(IntPtr window);

    [DllImport("user32.dll")]
    internal static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetClassName(IntPtr window, StringBuilder name, int capacity);

    [DllImport("user32.dll")]
    internal static extern bool GetWindowRect(IntPtr window, out RECT bounds);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out RECT value, int size);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left, Top, Right, Bottom;
        public readonly System.Drawing.Rectangle ToRectangle() =>
            System.Drawing.Rectangle.FromLTRB(Left, Top, Right, Bottom);
    }

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("user32.dll")]
    internal static extern IntPtr MonitorFromPoint(POINT pt, uint flags);

    [DllImport("dxva2.dll", SetLastError = true)]
    internal static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint pdwNumberOfPhysicalMonitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    internal static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint dwPhysicalMonitorArraySize, [Out] PHYSICAL_MONITOR[] physicalMonitorArray);

    [DllImport("dxva2.dll", SetLastError = true)]
    internal static extern bool DestroyPhysicalMonitors(uint dwPhysicalMonitorArraySize, PHYSICAL_MONITOR[] physicalMonitorArray);

    [DllImport("dxva2.dll", SetLastError = true)]
    internal static extern bool GetMonitorBrightness(IntPtr hMonitor, out uint pdwMinimumBrightness, out uint pdwCurrentBrightness, out uint pdwMaximumBrightness);

    [DllImport("dxva2.dll", SetLastError = true)]
    internal static extern bool SetMonitorBrightness(IntPtr hMonitor, uint dwNewBrightness);

    [DllImport("dxva2.dll", SetLastError = true)]
    internal static extern bool GetMonitorCapabilities(IntPtr hMonitor, out uint capabilities, out uint colorTemperatures);

    [DllImport("dxva2.dll", SetLastError = true)]
    internal static extern bool GetMonitorColorTemperature(IntPtr hMonitor, out uint temperature);

    [DllImport("dxva2.dll", SetLastError = true)]
    internal static extern bool GetMonitorRedGreenOrBlueGain(IntPtr hMonitor, uint channel, out uint min, out uint current, out uint max);

    [DllImport("dxva2.dll", SetLastError = true)]
    internal static extern bool GetVCPFeatureAndVCPFeatureReply(IntPtr hMonitor, byte code, IntPtr type, out uint current, out uint max);

    [DllImport("dxva2.dll", SetLastError = true)]
    internal static extern bool SetVCPFeature(IntPtr hMonitor, byte code, uint value);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct PHYSICAL_MONITOR
    {
        public IntPtr hPhysicalMonitor;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szPhysicalMonitorDescription;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern bool EnumDisplayDevices(string deviceName, uint deviceIndex, ref DISPLAY_DEVICE device, uint flags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DISPLAY_DEVICE
    {
        public uint cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;

        public POINT(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static implicit operator POINT(System.Drawing.Point p) => new POINT(p.X, p.Y);
    }
}
