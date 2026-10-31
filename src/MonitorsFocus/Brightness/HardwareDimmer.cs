using MonitorsFocus.Monitoring;

namespace MonitorsFocus.Brightness;

internal sealed class HardwareDimmer
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _refreshGate = new(1);
    private readonly Dictionary<string, BrightnessSession> _sessions = new();
    private readonly Dictionary<string, Capability> _capabilities = new();
    private long _generation;
    private bool _closed;

    public IReadOnlyDictionary<string, Capability> Capabilities
    {
        get { lock (_gate) return new Dictionary<string, Capability>(_capabilities); }
    }

    public async Task RefreshAsync(IEnumerable<MonitorInfo> monitors, IReadOnlyCollection<string> disabledIds, bool enabled)
    {
        var snapshot = monitors.ToArray();
        var disabled = disabledIds.ToHashSet();
        long generation;
        lock (_gate)
        {
            if (_closed) return;
            generation = ++_generation;
            _capabilities.Clear();
        }
        await _refreshGate.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (_gate) { if (_closed || generation != _generation) return; }
            await CloseSessionsAsync().ConfigureAwait(false);
            foreach (var monitor in snapshot)
            {
                lock (_gate) { if (_closed || generation != _generation) return; }
                if (!enabled || disabled.Contains(monitor.Id))
                {
                    Publish(generation, monitor.Id, Capability.DisabledBySettings());
                    continue;
                }
                Publish(generation, monitor.Id, new Capability(DimmingStatus.Detecting));
                IBrightnessDevice? device = null;
                try
                {
                    device = await Task.Run(() => DdcBrightnessDevice.Open(monitor)
                        ?? WmiBrightnessController.Open(DisplayIdentity.FindInstance(monitor.Id))).ConfigureAwait(false);
                    if (device == null)
                    {
                        Publish(generation, monitor.Id, Capability.Unsupported("No safely mapped brightness device."));
                        continue;
                    }
                    var session = new BrightnessSession(device, error =>
                    {
                        if (error != null) Publish(generation, monitor.Id, Capability.Failed(error));
                    });
                    bool accepted;
                    lock (_gate)
                    {
                        accepted = !_closed && generation == _generation;
                        if (accepted) _sessions[monitor.Id] = session;
                    }
                    if (!accepted) { await session.CloseAsync().ConfigureAwait(false); return; }
                    device = null; // Ownership has moved to the session.
                    Publish(generation, monitor.Id, Capability.Supported());
                }
                catch (Exception ex)
                {
                    device?.Dispose();
                    Publish(generation, monitor.Id, Capability.Failed(ex.Message));
                }
            }
        }
        finally { _refreshGate.Release(); }
    }

    public bool CanDim(string id)
    {
        lock (_gate) return !_closed && _sessions.TryGetValue(id, out var session) && session.CanDim;
    }

    public void Dim(string id, int percent)
    {
        lock (_gate)
            if (!_closed && _sessions.TryGetValue(id, out var session)) _ = session.DimAsync(percent);
    }

    public void Restore(string id)
    {
        lock (_gate)
            if (_sessions.TryGetValue(id, out var session)) _ = session.RestoreAsync();
    }

    public void RestoreAll()
    {
        lock (_gate)
            foreach (var session in _sessions.Values) _ = session.RestoreAsync();
    }

    public async Task ShutdownAsync()
    {
        lock (_gate) { _closed = true; ++_generation; }
        await _refreshGate.WaitAsync().ConfigureAwait(false);
        try { await CloseSessionsAsync().ConfigureAwait(false); }
        finally { _refreshGate.Release(); }
    }

    private async Task CloseSessionsAsync()
    {
        BrightnessSession[] sessions;
        lock (_gate) { sessions = _sessions.Values.ToArray(); _sessions.Clear(); }
        await Task.WhenAll(sessions.Select(session => session.CloseAsync())).ConfigureAwait(false);
    }

    private void Publish(long generation, string id, Capability capability)
    {
        lock (_gate)
        {
            if (_closed || generation != _generation) return;
            _capabilities[id] = capability;
        }
        LogSink.Info($"Capability: {id} -> {capability.Status} ({capability.Message ?? "n/a"})");
    }

    internal sealed record Capability(DimmingStatus Status, string? Message = null)
    {
        public static Capability Supported() => new(DimmingStatus.Supported);
        public static Capability Unsupported(string? message = null) => new(DimmingStatus.Unsupported, message);
        public static Capability Failed(string? message = null) => new(DimmingStatus.Failed, message);
        public static Capability DisabledBySettings() => new(DimmingStatus.DisabledBySettings, "Disabled by settings");
    }

    internal enum DimmingStatus { Detecting, Supported, Unsupported, Failed, DisabledBySettings }
}
