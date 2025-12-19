using System;
using System.Drawing;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace MonitorsFocus.Monitoring;

internal sealed class MouseTracker : IDisposable
{
    private readonly Timer _timer;

    public MouseTracker(int intervalMs)
    {
        _timer = new Timer
        {
            Interval = intervalMs
        };
        _timer.Tick += OnTick;
    }

    public event Action<Point, Screen>? Tick;

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    private void OnTick(object? sender, EventArgs e)
    {
        var position = Cursor.Position;
        var screen = Screen.FromPoint(position);
        Tick?.Invoke(position, screen);
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer.Dispose();
    }
}
