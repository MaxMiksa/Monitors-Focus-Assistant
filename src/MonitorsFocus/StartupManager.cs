using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MonitorsFocus;

internal static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static void Apply(string appName, bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key == null)
            {
                return;
            }

            if (enabled)
            {
                key.SetValue(appName, Application.ExecutablePath);
            }
            else
            {
                key.DeleteValue(appName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Ignore registry failures to avoid crashing the app.
        }
    }
}
