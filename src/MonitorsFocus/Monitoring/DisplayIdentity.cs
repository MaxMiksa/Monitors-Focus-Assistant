using System.Runtime.InteropServices;

namespace MonitorsFocus.Monitoring;

internal static class DisplayIdentity
{
    public static string? FindInstance(string displayName)
    {
        string? result = null;
        for (uint index = 0; ; index++)
        {
            var device = new NativeMethods.DISPLAY_DEVICE
            {
                cb = (uint)Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>()
            };
            if (!NativeMethods.EnumDisplayDevices(displayName, index, ref device, 1)) break;
            if ((device.StateFlags & 1) == 0) continue;
            var instance = FromInterfacePath(device.DeviceID);
            if (instance == null || result != null) return null;
            result = instance;
        }
        return result;
    }

    internal static string? FromInterfacePath(string path)
    {
        var parts = path.Split('#');
        if (parts.Length != 4 || !parts[0].Equals(@"\\?\DISPLAY", StringComparison.OrdinalIgnoreCase)) return null;
        return $@"DISPLAY\{parts[1]}\{parts[2]}";
    }

    internal static bool MatchesWmi(string instance, string wmiName)
    {
        if (instance.Equals(wmiName, StringComparison.OrdinalIgnoreCase)) return true;
        // WMI appends an instance ordinal to the PnP instance path.
        if (!wmiName.StartsWith(instance + "_", StringComparison.OrdinalIgnoreCase)) return false;
        return uint.TryParse(wmiName.AsSpan(instance.Length + 1), out _);
    }
}
