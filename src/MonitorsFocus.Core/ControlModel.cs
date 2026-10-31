namespace MonitorsFocus.Core;

public sealed record DisplayState(string Id, string Name, int Brightness, bool CanAdjust,
    bool CanAdjustColor, int? Kelvin, string? Error, bool IsPrimary);
public sealed record ControlSnapshot(List<DisplayState> Displays, ControlSettings Settings,
    bool FocusPaused, string Source, string? Error = null, string? Warning = null);
public sealed record ControlRequest(string Action, string? Id = null, int? Value = null, ControlSettings? Settings = null);

public sealed class ControlSettings
{
    public int SchemaVersion { get; set; } = 1;
    public long Revision { get; set; }
    public bool Linked { get; set; }
    public Dictionary<string, double> LinkWeights { get; set; } = new();
    public bool FocusEnabled { get; set; }
    public int FocusDelaySeconds { get; set; } = 180;
    public int FocusBrightness { get; set; } = 10;
    public bool ScheduleEnabled { get; set; }
    public bool LaunchOnStartup { get; set; }
    public List<Scene> Scenes { get; set; } = new();
    public List<PlanPoint> Schedule { get; set; } = new();
}

public sealed class Scene
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "新场景";
    public Dictionary<string, DisplayTarget> Targets { get; set; } = new();
}

public sealed record DisplayTarget(int Brightness, int? Kelvin);
public sealed record PlanPoint(int MinuteOfDay, string SceneId, int TransitionMinutes);

public static class RatioBinding
{
    public static Dictionary<string, int> Calculate(IReadOnlyDictionary<string, double> weights, string anchor, int value)
    {
        if (!weights.TryGetValue(anchor, out var weight) || weight <= 0 || weights.Values.Any(x => x <= 0 || !double.IsFinite(x)))
            throw new ArgumentException("绑定前请为每块屏幕设置非零基准亮度。");
        var factor = Math.Min(Math.Clamp(value, 0, 100) / weight, 100 / weights.Values.Max());
        return weights.ToDictionary(x => x.Key, x => (int)Math.Round(x.Value * factor));
    }
}

public sealed record PlanResult(DateTime StartsAt, DateTime NextAt, string SceneName, Dictionary<string, DisplayTarget> Targets);

public static class DailyPlan
{
    public static void Validate(ControlSettings settings)
    {
        if (settings.SchemaVersion != 1) throw new ArgumentException("配置版本不受支持。");
        if (settings.FocusDelaySeconds is < 0 or > 600 || settings.FocusBrightness is < 0 or > 100)
            throw new ArgumentException("专注参数超出范围。");
        if (settings.Scenes.Select(x => x.Id).Distinct().Count() != settings.Scenes.Count)
            throw new ArgumentException("场景标识重复。");
        foreach (var scene in settings.Scenes)
        {
            if (string.IsNullOrWhiteSpace(scene.Name)) throw new ArgumentException("请为场景命名。");
            foreach (var target in scene.Targets.Values)
                if (target.Brightness is < 0 or > 100 || target.Kelvin is < 2700 or > 6500)
                    throw new ArgumentException("亮度或色温超出范围。");
        }
        var points = settings.Schedule.OrderBy(x => x.MinuteOfDay).ToArray();
        if (points.Select(x => x.MinuteOfDay).Distinct().Count() != points.Length)
            throw new ArgumentException("时间节点不能重复。");
        for (var i = 0; i < points.Length; i++)
        {
            var point = points[i];
            var gap = (points[(i + 1) % points.Length].MinuteOfDay - point.MinuteOfDay + 1440) % 1440;
            if (gap == 0) gap = 1440;
            if (point.MinuteOfDay is < 0 or >= 1440 || point.TransitionMinutes < 0 || point.TransitionMinutes > gap)
                throw new ArgumentException("渐变必须在下一个时间节点前完成。");
            if (!settings.Scenes.Any(s => s.Id == point.SceneId)) throw new ArgumentException("时间计划引用了不存在的场景。");
        }
    }

    public static PlanResult? Evaluate(ControlSettings settings, DateTime now)
    {
        if (!settings.ScheduleEnabled || settings.Schedule.Count == 0) return null;
        var events = Enumerable.Range(-1, 3).SelectMany(day => settings.Schedule.Select(p =>
            (At: now.Date.AddDays(day).AddMinutes(p.MinuteOfDay), Point: p))).OrderBy(x => x.At).ToArray();
        var index = Array.FindLastIndex(events, e => e.At <= now);
        var current = events[index];
        var previous = events[Math.Max(0, index - 1)];
        var scene = settings.Scenes.Single(s => s.Id == current.Point.SceneId);
        var prior = settings.Scenes.Single(s => s.Id == previous.Point.SceneId);
        var duration = current.Point.TransitionMinutes;
        var progress = duration == 0 ? 1 : Math.Clamp((now - current.At).TotalMinutes / duration, 0, 1);
        var targets = scene.Targets.ToDictionary(pair => pair.Key, pair =>
        {
            var from = prior.Targets.GetValueOrDefault(pair.Key, pair.Value);
            var brightness = (int)Math.Round(from.Brightness + (pair.Value.Brightness - from.Brightness) * progress);
            int? temperature = pair.Value.Kelvin;
            if (from.Kelvin.HasValue && temperature.HasValue)
                temperature = (int)Math.Round(from.Kelvin.Value + (temperature.Value - from.Kelvin.Value) * progress);
            return new DisplayTarget(brightness, temperature);
        });
        return new PlanResult(current.At, events[index + 1].At, scene.Name, targets);
    }
}
