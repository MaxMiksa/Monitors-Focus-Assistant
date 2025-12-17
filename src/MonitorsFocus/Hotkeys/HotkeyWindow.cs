using System;
using System.Windows.Forms;

namespace MonitorsFocus.Hotkeys;

internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    public HotkeyWindow()
    {
        CreateHandle(new CreateParams());
    }

    public event Action<int>? HotkeyPressed;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY)
        {
            HotkeyPressed?.Invoke(m.WParam.ToInt32());
        }

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        DestroyHandle();
    }
}
