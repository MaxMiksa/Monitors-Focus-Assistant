using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MonitorsFocus;

internal static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>资源管理器记录启动项开关状态的位置；条目被禁用时 Run 值仍在，但登录不会启动。</summary>
    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    /// <summary>登录自启唯一条目名，旧托盘与控制中心共用，任务管理器里只显示一项。</summary>
    public const string AppName = "MonitorsFocus";

    /// <summary>登录后启动常驻核心，不弹出面板。</summary>
    public const string CenterArguments = "--center --background";

    /// <summary>历史版本写入过的条目名，接管时一并清理，避免登录时两个进程抢单实例。</summary>
    private static readonly string[] ObsoleteNames = { "MonitorsFocus.ControlCenter" };

    /// <summary>启动时校正条目：配置为开启时补齐注册表路径或被系统禁用的状态，已正确则不写入。</summary>
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
                Approve(true);
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
                Approve(false);
            }

            foreach (var name in ObsoleteNames) key.DeleteValue(name, throwOnMissingValue: false);
        }
        catch
        {
            if (reportFailure) throw;
            // Ignore registry failures to avoid crashing the app.
        }
    }

    /// <summary>同步资源管理器的启动项开关状态：已有禁用记录时保持关闭，开启时改写为启用字节。</summary>
    private static void Approve(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: true);
        if (key == null) return; // 该键由资源管理器维护，缺失时系统按新条目处理。

        if (!enabled)
        {
            key.DeleteValue(AppName, throwOnMissingValue: false);
            return;
        }

        if (key.GetValue(AppName) is byte[] state && state.Length >= 1 && state[0] == 2) return;

        var bytes = new byte[12];
        bytes[0] = 2; // 2 为启用，3 为禁用。
        BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc()).CopyTo(bytes, 4);
        key.SetValue(AppName, bytes, RegistryValueKind.Binary);
    }

    private static string Expected(string arguments) => $"\"{Application.ExecutablePath}\" {arguments}".TrimEnd();
}
