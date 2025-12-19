using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace MonitorsFocus.Settings;

internal sealed class AppSettings
{
    public int DelaySeconds { get; set; } = 180;
    public int OverlayOpacity { get; set; } = 100;
    public List<string> ControlledMonitorIds { get; set; } = new();
    public HotkeySettings Hotkey { get; set; } = HotkeySettings.CreateDefault();
    public bool LaunchOnStartup { get; set; } = false;
    public bool EnableDdcCi { get; set; } = false;
    public int HardwareDimLevel { get; set; } = 10;
    public List<string> HardwareDimDisabledMonitorIds { get; set; } = new();
    public DimmingMode DimmingMode { get; set; } = DimmingMode.AutoPreferHardware;
    public UiLanguage Language { get; set; } = UiLanguage.English;
    public int LastWindowWidth { get; set; } = 1400;
    public int LastWindowHeight { get; set; } = 1000;

    public void Normalize()
    {
        DelaySeconds = Math.Clamp(DelaySeconds, 0, 600);
        OverlayOpacity = Math.Clamp(OverlayOpacity, 0, 100);
        HardwareDimLevel = Math.Clamp(HardwareDimLevel, 0, 100);
        LastWindowWidth = Math.Clamp(LastWindowWidth, 800, 2000);
        LastWindowHeight = Math.Clamp(LastWindowHeight, 600, 1600);
        ControlledMonitorIds ??= new List<string>();
        HardwareDimDisabledMonitorIds ??= new List<string>();
        Hotkey ??= HotkeySettings.CreateDefault();
    }

    public AppSettings Clone()
    {
        return new AppSettings
        {
            DelaySeconds = DelaySeconds,
            OverlayOpacity = OverlayOpacity,
            ControlledMonitorIds = new List<string>(ControlledMonitorIds ?? new List<string>()),
            HardwareDimDisabledMonitorIds = new List<string>(HardwareDimDisabledMonitorIds ?? new List<string>()),
            Hotkey = Hotkey.Clone(),
            LaunchOnStartup = LaunchOnStartup,
            EnableDdcCi = EnableDdcCi,
            HardwareDimLevel = HardwareDimLevel,
            DimmingMode = DimmingMode,
            Language = Language,
            LastWindowWidth = LastWindowWidth,
            LastWindowHeight = LastWindowHeight
        };
    }
}

internal sealed class HotkeySettings
{
    public bool Ctrl { get; set; } = true;
    public bool Alt { get; set; } = false;
    public bool Shift { get; set; } = false;
    public bool Win { get; set; } = false;
    public Keys Key { get; set; } = Keys.F12;

    public static HotkeySettings CreateDefault()
    {
        return new HotkeySettings
        {
            Ctrl = true,
            Alt = false,
            Shift = false,
            Win = false,
            Key = Keys.F12
        };
    }

    public HotkeySettings Clone()
    {
        return new HotkeySettings
        {
            Ctrl = Ctrl,
            Alt = Alt,
            Shift = Shift,
            Win = Win,
            Key = Key
        };
    }
}

internal enum DimmingMode
{
    AutoPreferHardware,
    OverlayOnly,
    HardwareOnly
}

internal enum UiLanguage
{
    English,
    Chinese
}
