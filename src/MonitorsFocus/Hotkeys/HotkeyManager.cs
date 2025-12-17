using System;
using MonitorsFocus.Settings;

namespace MonitorsFocus.Hotkeys;

internal sealed class HotkeyManager : IDisposable
{
    private const int HotkeyId = 1;
    private readonly HotkeyWindow _window;
    private bool _isRegistered;

    public HotkeyManager()
    {
        _window = new HotkeyWindow();
        _window.HotkeyPressed += OnHotkeyPressed;
    }

    public event Action? HotkeyPressed;

    public bool Register(HotkeySettings settings)
    {
        Unregister();

        var modifiers = NativeMethods.MOD_NOREPEAT;
        if (settings.Ctrl)
        {
            modifiers |= NativeMethods.MOD_CONTROL;
        }
        if (settings.Alt)
        {
            modifiers |= NativeMethods.MOD_ALT;
        }
        if (settings.Shift)
        {
            modifiers |= NativeMethods.MOD_SHIFT;
        }
        if (settings.Win)
        {
            modifiers |= NativeMethods.MOD_WIN;
        }

        _isRegistered = NativeMethods.RegisterHotKey(
            _window.Handle,
            HotkeyId,
            modifiers,
            (uint)settings.Key);

        return _isRegistered;
    }

    public void Unregister()
    {
        if (_isRegistered)
        {
            NativeMethods.UnregisterHotKey(_window.Handle, HotkeyId);
            _isRegistered = false;
        }
    }

    private void OnHotkeyPressed(int id)
    {
        if (id == HotkeyId)
        {
            HotkeyPressed?.Invoke();
        }
    }

    public void Dispose()
    {
        Unregister();
        _window.HotkeyPressed -= OnHotkeyPressed;
        _window.Dispose();
    }
}
