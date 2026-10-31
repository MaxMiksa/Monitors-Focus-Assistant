using System;
using System.Windows.Forms;

namespace MonitorsFocus;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var instance = new Mutex(true, @"Local\MonitorsFocus." + Environment.UserName, out var created);
        if (!created) return;
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Application.Run(args.Contains("--center")
            ? new ControlCenterContext(!args.Contains("--background"))
            : new MainAppContext());
    }
}
