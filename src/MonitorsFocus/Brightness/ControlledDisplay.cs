using MonitorsFocus.Core;

namespace MonitorsFocus.Brightness;

internal sealed class ControlledDisplay
{
    private readonly IBrightnessDevice _device;
    private volatile uint[]? _originalGains;
    private readonly object _gate = new();
    private Task _worker = Task.CompletedTask;
    private (int Normal, int Effective, int? Kelvin)? _pending;
    private bool _probeRequested;
    private int _lastBrightness;
    private int _normalBrightness;
    private int? _lastKelvin;
    private (uint Red, uint Green, uint Blue)? _lastGains;
    private int? _requestedKelvin;
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
        if (device is IColorDevice color) _originalGains = color.ReadGains();
        if (device is IColorDevice && _originalGains == null) StartColorRecovery();
    }

    public int InitialBrightness => _normalBrightness;
    public bool SupportsColor => _originalGains != null;
    public string? Error { get { lock (_gate) return _error; } }

    // The first gain read on a fresh DDC channel is unreliable on some monitors. Instead of
    // declaring hardware color unsupported for the whole session, keep probing in the background;
    // probes go through the same serialized worker so no two device commands overlap.
    private void StartColorRecovery()
    {
        _ = Task.Run(async () =>
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt == 0 ? 2 : 6)).ConfigureAwait(false);
                lock (_gate) { if (_closed || _failed || _originalGains != null) return; }
                RequestProbe();
            }
        });
    }

    private void RequestProbe()
    {
        lock (_gate)
        {
            if (_closed || _failed || _originalGains != null) return;
            _probeRequested = true;
            if (!_running) { _running = true; _worker = Task.Run(ProcessAsync); }
        }
    }

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
            bool probe;
            lock (_gate)
            {
                if (!_pending.HasValue && !_probeRequested) { _running = false; return; }
                probe = _probeRequested;
                _probeRequested = false;
                target = _pending ?? (_normalBrightness, _lastBrightness, _requestedKelvin);
                _pending = null;
            }
            var operation = Task.Run(() => Apply(target, probe));
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

    private void Apply((int Normal, int Effective, int? Kelvin) target, bool probe)
    {
        _requestedKelvin = target.Kelvin;
        if (probe && _originalGains == null && _device is IColorDevice probeTarget &&
            probeTarget.ReadGains() is { } gains)
            _originalGains = gains;
        _normalBrightness = target.Normal;
        if (_lastBrightness != target.Effective)
        {
            _temporaryBrightness |= target.Effective != target.Normal;
            _device.SetBrightness(target.Effective);
            _lastBrightness = target.Effective;
        }
        _temporaryBrightness = target.Effective != target.Normal;
        if (_device is not IColorDevice ddc || _originalGains is not { } baseline) return;
        if (!target.Kelvin.HasValue)
        {
            if (_colorOwned) ddc.RestoreGains(baseline);
            _colorOwned = false;
            _lastKelvin = null;
            _lastGains = null;
        }
        else if (_lastKelvin != target.Kelvin)
        {
            var warmGains = WarmGains(baseline, target.Kelvin.Value);
            if (_lastGains != warmGains)
            {
                _colorOwned = true;
                ddc.SetGains(new[] { warmGains.Red, warmGains.Green, warmGains.Blue });
                _lastGains = warmGains;
            }
            _lastKelvin = target.Kelvin;
        }
    }

    internal static (uint Red, uint Green, uint Blue) WarmGains(uint[] baseline, int kelvin)
    {
        // Approximate warmth relative to the user's native white point, not calibrated CCT.
        var warmth = (6500 - Math.Clamp(kelvin, 2700, 6500)) / 3800.0;
        return (baseline[0], (uint)Math.Round(baseline[1] * (1 - 0.28 * warmth)),
            (uint)Math.Round(baseline[2] * (1 - 0.65 * warmth)));
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
            if (_colorOwned && _device is IColorDevice ddc && _originalGains is { } baseline)
            {
                ddc.RestoreGains(baseline);
                _colorOwned = false;
                _lastKelvin = null;
                _lastGains = null;
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
