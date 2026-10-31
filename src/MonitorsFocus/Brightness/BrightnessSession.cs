namespace MonitorsFocus.Brightness;

// One worker owns a device, including its restore baseline and native handle lifetime.
internal sealed class BrightnessSession
{
    private readonly IBrightnessDevice _device;
    private readonly Action<string?> _report;
    private readonly int _timeoutMs;
    private readonly object _gate = new();
    private Task _worker = Task.CompletedTask;
    private Task? _closing;
    private bool _running;
    private bool _pending;
    private int? _target;
    private int? _baseline;
    private bool _disabled;
    private bool _closed;

    public BrightnessSession(IBrightnessDevice device, Action<string?> report, int timeoutMs = 800)
    {
        _device = device;
        _report = report;
        _timeoutMs = timeoutMs;
    }

    public bool CanDim { get { lock (_gate) return !_disabled && !_closed; } }

    public Task DimAsync(int percent) => Request(Math.Clamp(percent, 0, 100));
    public Task RestoreAsync() => Request(null);

    private Task Request(int? target)
    {
        lock (_gate)
        {
            if (_closed || (target.HasValue && _disabled)) return _worker;
            _target = target;
            _pending = true;
            if (!_running)
            {
                _running = true;
                _worker = Task.Run(ProcessAsync);
            }
            return _worker;
        }
    }

    private async Task ProcessAsync()
    {
        while (true)
        {
            int? target;
            lock (_gate)
            {
                if (!_pending)
                {
                    _running = false;
                    return;
                }
                target = _target;
                _pending = false;
                if (target.HasValue && _disabled) continue;
            }

            var operation = Task.Run(() => Apply(target));
            using var timeout = new CancellationTokenSource();
            var delay = Task.Delay(_timeoutMs, timeout.Token);
            if (await Task.WhenAny(operation, delay).ConfigureAwait(false) != operation)
            {
                lock (_gate)
                {
                    _disabled = true;
                    if (target.HasValue)
                    {
                        _target = null;
                        _pending = true;
                    }
                }
                Report("Device timed out; waiting for the in-flight call before restoring.");
            }
            timeout.Cancel();
            try
            {
                // A timeout never releases a handle or lets a newer write overtake this call.
                await operation.ConfigureAwait(false);
                Report(null);
            }
            catch (Exception ex)
            {
                lock (_gate) _disabled = true;
                Report(ex.Message);
            }
        }
    }

    private void Apply(int? target)
    {
        if (target.HasValue)
        {
            _baseline ??= _device.ReadBrightness();
            _device.SetBrightness(target.Value);
        }
        else if (_baseline is int original)
        {
            _device.SetBrightness(original);
            _baseline = null;
        }
    }

    public Task CloseAsync()
    {
        lock (_gate)
        {
            if (_closing != null) return _closing;
            _closed = true;
            _target = null;
            _pending = true;
            if (!_running)
            {
                _running = true;
                _worker = Task.Run(ProcessAsync);
            }
            _closing = CloseCoreAsync(_worker);
            return _closing;
        }
    }

    private async Task CloseCoreAsync(Task worker)
    {
        await worker.ConfigureAwait(false);
        try
        {
            if (_baseline.HasValue)
                throw new InvalidOperationException("The monitor could not be restored before closing.");
        }
        finally { _device.Dispose(); }
    }

    private void Report(string? error)
    {
        // Observers must not interrupt native resource cleanup.
        try { _report(error); }
        catch { }
    }
}
