using System;
using System.Management;

namespace MonitorsFocus.Brightness;

internal static class WmiBrightnessController
{
    public static bool TryGetBrightness(out byte current, out byte max)
    {
        current = 0;
        max = 0;
        try
        {
            using var searcher = new ManagementObjectSearcher("root\\wmi", "SELECT * FROM WmiMonitorBrightness");
            foreach (ManagementObject o in searcher.Get())
            {
                current = (byte)o.GetPropertyValue("CurrentBrightness");
                var levels = (byte[])o.GetPropertyValue("Level");
                if (levels is { Length: > 0 })
                {
                    max = levels[^1];
                }
                else
                {
                    max = 100;
                }
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    public static bool TrySetBrightness(byte target)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("root\\wmi", "SELECT * FROM WmiMonitorBrightnessMethods");
            foreach (ManagementObject o in searcher.Get())
            {
                o.InvokeMethod("WmiSetBrightness", new object[] { 1, target });
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }
}
