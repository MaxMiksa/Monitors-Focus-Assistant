using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MonitorsFocus.Monitoring;
using MonitorsFocus;

namespace MonitorsFocus.Brightness;

internal sealed class HardwareDimmer : IDisposable
{
    private readonly Dictionary<string, MonitorSession> _sessions = new();
    private readonly Dictionary<string, Capability> _capabilities = new();

    public IReadOnlyDictionary<string, Capability> Capabilities => _capabilities;

    public void Refresh(IEnumerable<MonitorInfo> monitors, IReadOnlyCollection<string> disabledIds)
    {
        RestoreAll();
        DisposeSessions();
        _capabilities.Clear();

        foreach (var monitor in monitors)
        {
            if (disabledIds.Contains(monitor.Id))
            {
                _capabilities[monitor.Id] = Capability.DisabledBySettings();
                continue;
            }

            var session = MonitorSession.Create(monitor);
            _sessions[monitor.Id] = session;
            _capabilities[monitor.Id] = session.Supported
                ? Capability.Supported()
                : Capability.Failed(session.FailureMessage ?? "Not supported");
            LogSink.Info($"Capability: {monitor.Id} => {_capabilities[monitor.Id].Status} ({_capabilities[monitor.Id].Message ?? "n/a"})");
        }
    }

    public bool IsSupported(string monitorId)
    {
        return _sessions.TryGetValue(monitorId, out var session) && session.Supported;
    }

    public bool CanDim(string monitorId)
    {
        return _sessions.TryGetValue(monitorId, out var session) && session.Supported && !session.DisabledThisSession;
    }

    public void Dim(string monitorId, int targetPercent, int timeoutMs = 800)
    {
        if (!_sessions.TryGetValue(monitorId, out var session) || !session.Supported || session.DisabledThisSession)
        {
            LogSink.Info($"Hardware dim skipped: {monitorId} (supported={session?.Supported}, disabledSession={session?.DisabledThisSession})");
            return;
        }

        RunWithTimeout(session, () => session.Dim(targetPercent), monitorId, timeoutMs);
    }

    public void Restore(string monitorId, int timeoutMs = 800)
    {
        if (_sessions.TryGetValue(monitorId, out var session) && session.Supported && !session.DisabledThisSession)
        {
            RunWithTimeout(session, session.Restore, monitorId, timeoutMs);
        }
    }

    public void RestoreAll(int timeoutMs = 800)
    {
        foreach (var session in _sessions.Values)
        {
            if (session.Supported && !session.DisabledThisSession)
            {
                RunWithTimeout(session, session.Restore, session.MonitorId, timeoutMs);
            }
        }
    }

    public void Dispose()
    {
        RestoreAll();
        DisposeSessions();
    }

    private void DisposeSessions()
    {
        foreach (var session in _sessions.Values)
        {
            session.Dispose();
        }
        _sessions.Clear();
    }

    private void RunWithTimeout(MonitorSession session, Action action, string monitorId, int timeoutMs)
    {
        var task = Task.Run(() =>
        {
            try
            {
                action();
                if (session.DisabledThisSession)
                {
                    SetCapability(monitorId, Capability.Failed(session.FailureMessage ?? "Disabled"));
                }
                else
                {
                    LogSink.Info($"Hardware dim op success: {monitorId}");
                }
            }
            catch (Exception ex)
            {
                session.DisabledThisSession = true;
                SetCapability(monitorId, Capability.Failed(ex.Message));
                LogSink.Info($"Hardware dim op failed: {monitorId} -> {ex.Message}");
            }
        });

        Task.Run(async () =>
        {
            await Task.Delay(timeoutMs);
            if (!task.IsCompleted)
            {
                session.DisabledThisSession = true;
                SetCapability(monitorId, Capability.Failed("Timed out"));
                LogSink.Info($"Hardware dim op timeout: {monitorId}");
            }
        });
    }

    private void SetCapability(string monitorId, Capability capability)
    {
        lock (_capabilities)
        {
            _capabilities[monitorId] = capability;
        }
    }

    internal sealed class Capability
    {
        public Capability(DimmingStatus status, string? message = null)
        {
            Status = status;
            Message = message;
        }

        public DimmingStatus Status { get; }
        public string? Message { get; }

        public static Capability Supported() => new(DimmingStatus.Supported);
        public static Capability Unsupported(string? msg = null) => new(DimmingStatus.Unsupported, msg);
        public static Capability Failed(string? msg = null) => new(DimmingStatus.Failed, msg);
        public static Capability DisabledBySettings() => new(DimmingStatus.DisabledBySettings, "Disabled by settings");
    }

    internal enum DimmingStatus
    {
        Supported,
        Unsupported,
        Failed,
        DisabledBySettings
    }

    private sealed class MonitorSession : IDisposable
    {
        private readonly MonitorInfo _monitor;
        private readonly NativeMethods.PHYSICAL_MONITOR[]? _handles;
        private readonly uint _min;
        private readonly uint _max;
        private readonly uint _original;
        private readonly bool _useWmi;

        private MonitorSession(MonitorInfo monitor, NativeMethods.PHYSICAL_MONITOR[] handles, uint min, uint max, uint original)
        {
            _monitor = monitor;
            _handles = handles;
            _min = min;
            _max = max;
            _original = original;
            Supported = true;
        }

        private MonitorSession(MonitorInfo monitor, uint min, uint max, uint original, bool useWmi)
        {
            _monitor = monitor;
            _min = min;
            _max = max;
            _original = original;
            _useWmi = useWmi;
            Supported = true;
        }

        public bool Supported { get; private set; }
        public bool DisabledThisSession { get; set; }
        public string MonitorId => _monitor.Id;
        public string? FailureMessage { get; private set; }

        public static MonitorSession Create(MonitorInfo monitor)
        {
            try
            {
                if (monitor.Handle == IntPtr.Zero)
                {
                    return Unsupported(monitor);
                }

                if (!NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(monitor.Handle, out var count) || count == 0)
                {
                    LogSink.Info($"DDC/CI: no physical monitors for {monitor.Id}, trying WMI.");
                    return TryWmiFallback(monitor);
                }

                var monitors = new NativeMethods.PHYSICAL_MONITOR[count];
                if (!NativeMethods.GetPhysicalMonitorsFromHMONITOR(monitor.Handle, count, monitors))
                {
                    LogSink.Info($"DDC/CI: cannot get physical monitors for {monitor.Id}, trying WMI.");
                    return TryWmiFallback(monitor, monitors);
                }

                var handle = monitors[0].hPhysicalMonitor;
                if (!NativeMethods.GetMonitorBrightness(handle, out var min, out var current, out var max))
                {
                    LogSink.Info($"DDC/CI: cannot get brightness for {monitor.Id}, trying WMI.");
                    return TryWmiFallback(monitor, monitors);
                }

                LogSink.Info($"DDC/CI: supported {monitor.Id} range {min}-{max} current {current}");
                return new MonitorSession(monitor, monitors, min, max, current);
            }
            catch
            {
                LogSink.Info($"DDC/CI: exception creating session for {monitor.Id}, trying WMI.");
                return TryWmiFallback(monitor);
            }
        }

        public void Dim(int targetPercent)
        {
            if (!Supported || (_handles == null || _handles.Length == 0) && !_useWmi)
            {
                return;
            }

            var clamped = Math.Clamp(targetPercent, 0, 100);
            var range = _max - _min;
            var value = _min + (uint)Math.Round(range * clamped / 100.0);
            value = Math.Clamp(value, _min, _max);

            try
            {
                if (_useWmi)
                {
                    var ok = WmiBrightnessController.TrySetBrightness((byte)value);
                    if (!ok)
                    {
                        FailureMessage = "WMI set failed";
                        DisabledThisSession = true;
                    }
                }
                else if (_handles != null && _handles.Length > 0)
                {
                    var ok = NativeMethods.SetMonitorBrightness(_handles[0].hPhysicalMonitor, value);
                    if (!ok)
                    {
                        FailureMessage = "DDC/CI set failed";
                        DisabledThisSession = true;
                    }
                }
            }
            catch
            {
                // Ignore; fallback will remain overlay-only.
            }
        }

        public void Restore()
        {
            if (!Supported)
            {
                return;
            }

            try
            {
                if (_useWmi)
                {
                    var ok = WmiBrightnessController.TrySetBrightness((byte)_original);
                    if (!ok)
                    {
                        FailureMessage = "WMI restore failed";
                        DisabledThisSession = true;
                    }
                }
                else if (_handles != null && _handles.Length > 0)
                {
                    var ok = NativeMethods.SetMonitorBrightness(_handles[0].hPhysicalMonitor, _original);
                    if (!ok)
                    {
                        FailureMessage = "DDC/CI restore failed";
                        DisabledThisSession = true;
                    }
                }
            }
            catch
            {
                FailureMessage = "Restore failed";
            }
        }

        public void Dispose()
        {
            if (_handles != null && _handles.Length > 0)
            {
                NativeMethods.DestroyPhysicalMonitors((uint)_handles.Length, _handles);
            }
        }

        private static MonitorSession TryWmiFallback(MonitorInfo monitor, NativeMethods.PHYSICAL_MONITOR[]? handles = null)
        {
            if (handles != null && handles.Length > 0)
            {
                try
                {
                    NativeMethods.DestroyPhysicalMonitors((uint)handles.Length, handles);
                }
                catch
                {
                    // best-effort cleanup
                }
            }

            if (WmiBrightnessController.TryGetBrightness(out var current, out var max))
            {
                LogSink.Info($"WMI: supported {monitor.Id} range 0-{max} current {current}");
                return new MonitorSession(monitor, 0, max, current, true);
            }

            LogSink.Info($"WMI: not supported for {monitor.Id}");
            return Unsupported(monitor, null, "Not supported");
        }

        private static MonitorSession Unsupported(MonitorInfo monitor, NativeMethods.PHYSICAL_MONITOR[]? handles = null, string? message = null)
        {
            if (handles != null && handles.Length > 0)
            {
                try
                {
                    NativeMethods.DestroyPhysicalMonitors((uint)handles.Length, handles);
                }
                catch
                {
                    // best-effort cleanup
                }
            }

            return new MonitorSession(monitor, Array.Empty<NativeMethods.PHYSICAL_MONITOR>(), 0, 0, 0)
            {
                Supported = false,
                FailureMessage = message
            };
        }
    }
}
