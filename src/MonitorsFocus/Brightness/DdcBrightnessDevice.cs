using System.ComponentModel;
using System.Runtime.InteropServices;
using MonitorsFocus.Monitoring;

namespace MonitorsFocus.Brightness;

internal sealed class DdcBrightnessDevice : IColorDevice
{
    private readonly NativeMethods.PHYSICAL_MONITOR[] _handles;
    private readonly uint _min;
    private readonly uint _max;

    private DdcBrightnessDevice(NativeMethods.PHYSICAL_MONITOR[] handles, uint min, uint max)
    {
        _handles = handles;
        _min = min;
        _max = max;
    }

    public static IBrightnessDevice? Open(MonitorInfo monitor)
    {
        if (monitor.Handle == IntPtr.Zero ||
            !NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(monitor.Handle, out var count) || count != 1)
            return null;

        var handles = new NativeMethods.PHYSICAL_MONITOR[count];
        if (!NativeMethods.GetPhysicalMonitorsFromHMONITOR(monitor.Handle, count, handles)) return null;
        var transferred = false;
        try
        {
            if (!NativeMethods.GetMonitorBrightness(handles[0].hPhysicalMonitor, out var min, out _, out var max) || max <= min)
                return null;
            var device = new DdcBrightnessDevice(handles, min, max);
            transferred = true;
            return device;
        }
        finally
        {
            if (!transferred) NativeMethods.DestroyPhysicalMonitors(count, handles);
        }
    }

    // DDC/CI over I2C occasionally NAKs a command (seen on AOC U27G4); retry after the MCCS settle time.
    private static bool Retry(Func<bool> command)
    {
        for (var attempt = 0; ; attempt++)
        {
            if (command()) return true;
            if (attempt == 2) return false;
            Thread.Sleep(50);
        }
    }

    public int ReadBrightness()
    {
        uint value = 0;
        if (!Retry(() => NativeMethods.GetMonitorBrightness(_handles[0].hPhysicalMonitor, out _, out value, out _)))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot read monitor brightness.");
        return Math.Clamp((int)Math.Round((value - (double)_min) * 100 / (_max - _min)), 0, 100);
    }

    public void SetBrightness(int percent)
    {
        var value = _min + (uint)Math.Round((_max - _min) * Math.Clamp(percent, 0, 100) / 100.0);
        if (!Retry(() => NativeMethods.SetMonitorBrightness(_handles[0].hPhysicalMonitor, value)))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot set monitor brightness.");
    }

    public void Dispose() => NativeMethods.DestroyPhysicalMonitors((uint)_handles.Length, _handles);

    // MCCS video gain codes (red, green, blue). The high-level dxva2 gain setter reports success
    // on some monitors (e.g. AOC U27G4) without changing the picture, so write VCP codes directly.
    private static readonly byte[] GainCodes = { 0x16, 0x18, 0x1A };
    // Some monitors (AOC U27G4) ignore gains outside the "User" color preset (VCP 0x14 = 0x0B).
    private const byte ColorPresetCode = 0x14;
    private const uint UserPreset = 0x0B;
    private uint? _originalPreset;
    private bool _presetSwitched;

    public uint[]? ReadGains()
    {
        if (NativeMethods.GetVCPFeatureAndVCPFeatureReply(_handles[0].hPhysicalMonitor, ColorPresetCode, IntPtr.Zero, out var preset, out _))
            _originalPreset = preset;
        var values = new uint[3];
        for (var channel = 0; channel < 3; channel++)
            if (!Retry(() => NativeMethods.GetVCPFeatureAndVCPFeatureReply(_handles[0].hPhysicalMonitor, GainCodes[channel], IntPtr.Zero, out values[channel], out _)))
                return null;
        return values;
    }

    public void SetGains(uint[] values)
    {
        if (!_presetSwitched && _originalPreset is { } preset && preset != UserPreset)
        {
            if (!Retry(() => NativeMethods.SetVCPFeature(_handles[0].hPhysicalMonitor, ColorPresetCode, UserPreset)))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot switch monitor color preset.");
            _presetSwitched = true;
            Thread.Sleep(100);
        }
        for (var channel = 0; channel < 3; channel++)
            if (!Retry(() => NativeMethods.SetVCPFeature(_handles[0].hPhysicalMonitor, GainCodes[channel], values[channel])))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot set monitor color gain.");
    }

    public void RestoreGains(uint[] values)
    {
        SetGains(values);
        if (!_presetSwitched || _originalPreset is not { } preset) return;
        if (!Retry(() => NativeMethods.SetVCPFeature(_handles[0].hPhysicalMonitor, ColorPresetCode, preset)))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot restore monitor color preset.");
        _presetSwitched = false;
    }
}
