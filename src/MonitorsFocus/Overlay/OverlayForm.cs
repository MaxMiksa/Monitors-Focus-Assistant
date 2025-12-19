using System;
using System.Drawing;
using System.Windows.Forms;

namespace MonitorsFocus.Overlay;

internal sealed class OverlayForm : Form
{
    public OverlayForm(Rectangle bounds, int opacityPercent)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Black;
        AllowTransparency = true;
        Bounds = bounds;
        ApplyOpacity(opacityPercent);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var createParams = base.CreateParams;
            createParams.ExStyle |= NativeMethods.WS_EX_LAYERED
                                    | NativeMethods.WS_EX_TRANSPARENT
                                    | NativeMethods.WS_EX_TOOLWINDOW
                                    | NativeMethods.WS_EX_NOACTIVATE;
            return createParams;
        }
    }

    protected override bool ShowWithoutActivation => true;

    public void ApplyBounds(Rectangle bounds)
    {
        Bounds = bounds;
    }

    public void ApplyOpacity(int opacityPercent)
    {
        var clamped = Math.Clamp(opacityPercent, 0, 100);
        var value = clamped >= 100 ? 0.999 : clamped / 100.0;
        Opacity = value;
    }

    public void ShowOverlay()
    {
        if (!Visible)
        {
            Show();
        }

        NativeMethods.SetWindowPos(
            Handle,
            NativeMethods.HWND_TOPMOST,
            0,
            0,
            0,
            0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
    }

    public void HideOverlay()
    {
        if (Visible)
        {
            Hide();
        }
    }
}
