using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MonitorsFocus;

internal static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>登录自启唯一条目名，旧托盘与控制中心共用，任务管理器里只显示一项。</summary>
    public const string AppName = "MonitorsFocus";

    /// <summary>登录后启动常驻核心，不弹出面板。</summary>
    public const string CenterArguments = "--center --background";

    /// <summary>历史版本写入过的条目名，接管时一并清理，避免登录时两个进程抢单实例。</summary>
    private static readonly string[] ObsoleteNames = { "MonitorsFocus.ControlCenter" };

    /// <summary>启动时校正条目：仅在配置为开启且当前值与预期不符时写入，避免每次启动都改注册表。</summary>
    public static void Ensure(bool enabled, string arguments = CenterArguments) => Apply(enabled, arguments);

    public static void Apply(bool enabled, string arguments = "", bool reportFailure = false)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key == null)
            {
                if (reportFailure) throw new InvalidOperationException("无法访问登录自启动设置。");
                return;
            }

            if (enabled)
            {
                var expected = Expected(arguments);
                if ((key.GetValue(AppName) as string) != expected) key.SetValue(AppName, expected);
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
            }

            foreach (var name in ObsoleteNames) key.DeleteValue(name, throwOnMissingValue: false);
        }
        catch
        {
            if (reportFailure) throw;
            // Ignore registry failures to avoid crashing the app.
        }
    }

    private static string Expected(string arguments) => $"\"{Application.ExecutablePath}\" {arguments}".TrimEnd();
}
