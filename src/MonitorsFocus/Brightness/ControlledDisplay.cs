using MonitorsFocus.Core;

namespace MonitorsFocus.Brightness;

internal sealed class ControlledDisplay
{
    private readonly IBrightnessDevice _device;
    private readonly uint[]? _originalGains;
    private readonly object _gate = new();
    private Task _worker = Task.CompletedTask;
    private (int Normal, int Effective, int? Kelvin)? _pending;
    private int _lastBrightness;
    private int _normalBrightness;
    private int? _lastKelvin;
    private bool _temporaryBrightness;
    private bool _colorOwned;
    private bool _running;
    private bool _closed;
    private bool _failed;
    private Task? _closeTask;
    private string? _error;

    public ControlledDisplay(IBrightnessDevice device)
    {
        _device = device;
        _normalBrightness = _lastBrightness = device.ReadBrightness();
        if (device is DdcBrightnessDevice ddc) _originalGains = ddc.ReadGains();
    }

    public int InitialBrightness => _normalBrightness;
    public bool SupportsColor => _originalGains != null;
    public string? Error { get { lock (_gate) return _error; } }

    public Task ApplyAsync(int normal, int effective, int? kelvin)
    {
        lock (_gate)
        {
            if (_closed || _failed) return _worker;
            _pending = (normal, effective, kelvin);
            if (!_running) { _running = true; _worker = Task.Run(ProcessAsync); }
            return _worker;
        }
    }

    private async Task ProcessAsync()
    {
        while (true)
        {
            (int Normal, int Effective, int? Kelvin) target;
            lock (_gate)
            {
                if (!_pending.HasValue) { _running = false; return; }
                target = _pending.Value;
                _pending = null;
            }
            var operation = Task.Run(() => Apply(target));
            try
            {
                await operation.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                lock (_gate) { _failed = true; _pending = null; _error = ex is TimeoutException ? "设备响应超时，等待恢复" : ex.Message; }
                // Never race recovery or handle release against an uncancelled native call.
                try { await operation.ConfigureAwait(false); } catch { }
                try { Restore(); }
                catch (Exception restoreError) { lock (_gate) _error = $"恢复失败：{restoreError.Message}"; }
                lock (_gate) { _running = false; return; }
            }
        }
    }

    private void Apply((int Normal, int Effective, int? Kelvin) target)
    {
        _normalBrightness = target.Normal;
        if (_lastBrightness != target.Effective)
        {
            _temporaryBrightness |= target.Effective != target.Normal;
            _device.SetBrightness(target.Effective);
            _lastBrightness = target.Effective;
            _temporaryBrightness = target.Effective != target.Normal;
        }
        if (_device is not DdcBrightnessDevice ddc || _originalGains == null) return;
        if (!target.Kelvin.HasValue)
        {
            if (_colorOwned) ddc.RestoreGains(_originalGains);
            _colorOwned = false;
            _lastKelvin = null;
        }
        else if (_lastKelvin != target.Kelvin)
        {
            _colorOwned = true;
            ddc.SetGains(WarmGains(_originalGains, target.Kelvin.Value));
            _lastKelvin = target.Kelvin;
        }
    }

    internal static uint[] WarmGains(uint[] baseline, int kelvin)
    {
        // Approximate warmth relative to the user's native white point, not calibrated CCT.
        var warmth = (6500 - Math.Clamp(kelvin, 2700, 6500)) / 3800.0;
        return new[] { baseline[0], (uint)Math.Round(baseline[1] * (1 - 0.28 * warmth)),
            (uint)Math.Round(baseline[2] * (1 - 0.65 * warmth)) };
    }

    private void Restore()
    {
        Exception? failure = null;
        try
        {
            if (_temporaryBrightness)
            {
                _device.SetBrightness(_normalBrightness);
                _temporaryBrightness = false;
                _lastBrightness = _normalBrightness;
            }
        }
        catch (Exception ex) { failure = ex; }
        try
        {
            if (_colorOwned && _device is DdcBrightnessDevice ddc && _originalGains != null)
            {
                ddc.RestoreGains(_originalGains);
                _colorOwned = false;
                _lastKelvin = null;
            }
        }
        catch (Exception ex) { failure ??= ex; }
        if (failure != null) throw failure;
    }

    public Task CloseAsync()
    {
        lock (_gate)
        {
            if (_closeTask != null) return _closeTask;
            _closed = true;
            _pending = null;
            _closeTask = CloseCoreAsync(_worker);
            return _closeTask;
        }
    }

    private async Task CloseCoreAsync(Task worker)
    {
        await worker.ConfigureAwait(false);
        try { await Task.Run(Restore).ConfigureAwait(false); }
        finally { _device.Dispose(); }
    }
}
