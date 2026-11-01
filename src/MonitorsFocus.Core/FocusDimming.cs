namespace MonitorsFocus.Core;

public sealed class FocusDimming
{
    private long? _leftAt;

    public void Reset() => _leftAt = null;

    public bool Update(ControlSettings settings, bool paused, bool isPrimary, bool cursorOnDisplay,
        long now, string? displayId = null)
    {
        if (!settings.FocusEnabled || paused || isPrimary || cursorOnDisplay ||
            (settings.ControlledDisplayIds != null && (displayId == null || !settings.ControlledDisplayIds.Contains(displayId))))
            Reset();
        else _leftAt ??= now;
        return _leftAt.HasValue && now - _leftAt >= settings.FocusDelaySeconds * 1000L;
    }
}
