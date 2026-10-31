using Microsoft.Win32;

namespace MonitorsFocus;

internal static class AppIcons
{
    // 托盘图标跟随任务栏深浅色：浅色任务栏用深色图形，反之用白色图形。
    public static Icon Tray()
    {
        var light = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", 0) is 1;
        using var stream = typeof(AppIcons).Assembly.GetManifestResourceStream(light ? "MonitorsFocus.Assets.tray-light.ico" : "MonitorsFocus.Assets.tray-dark.ico")!;
        return new Icon(stream, SystemInformation.SmallIconSize);
    }
}
