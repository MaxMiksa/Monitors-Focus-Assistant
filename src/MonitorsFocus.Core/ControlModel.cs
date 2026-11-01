namespace MonitorsFocus.Core;

public sealed record DisplayState(string Id, string Name, int Brightness, bool CanAdjust,
    bool CanAdjustColor, int? Kelvin, string? Error, bool IsPrimary);
public sealed record ControlSnapshot(List<DisplayState> Displays, ControlSettings Settings,
    bool FocusPaused, string Source, string? Error = null, string? Warning = null, string? SystemLanguage = null);
public sealed record ControlRequest(string Action, string? Id = null, int? Value = null, ControlSettings? Settings = null);

public sealed class ControlSettings
{
    public int SchemaVersion { get; set; } = 1;
    public long Revision { get; set; }
    public string Language { get; set; } = "system";
    public bool Linked { get; set; }
    public Dictionary<string, double> LinkWeights { get; set; } = new();
    public bool FocusEnabled { get; set; }
    public List<string>? ControlledDisplayIds { get; set; }
    public bool KeepSecondaryAwakeOnFullscreen { get; set; } = true;
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
    public string? DefaultName { get; set; }
    public Dictionary<string, DisplayTarget> Targets { get; set; } = new();
    public SceneBehavior? Behavior { get; set; }
}

public sealed class SceneBehavior
{
    public bool Linked { get; set; }
    public Dictionary<string, double> LinkWeights { get; set; } = new();
    public bool FocusEnabled { get; set; }
    // null follows all secondary displays; an empty list controls none.
    public List<string>? ControlledDisplayIds { get; set; }

    public static SceneBehavior Capture(ControlSettings settings) => new()
    {
        Linked = settings.Linked, LinkWeights = new(settings.LinkWeights),
        FocusEnabled = settings.FocusEnabled, ControlledDisplayIds = settings.ControlledDisplayIds?.ToList()
    };

    public bool ApplyTo(ControlSettings settings)
    {
        var changed = settings.Linked != Linked || settings.FocusEnabled != FocusEnabled ||
            settings.LinkWeights.Count != LinkWeights.Count ||
            LinkWeights.Any(pair => !settings.LinkWeights.TryGetValue(pair.Key, out var value) || value != pair.Value) ||
            (ControlledDisplayIds == null ? settings.ControlledDisplayIds != null :
                settings.ControlledDisplayIds == null || !ControlledDisplayIds.ToHashSet().SetEquals(settings.ControlledDisplayIds));
        settings.Linked = Linked;
        settings.LinkWeights = new(LinkWeights);
        settings.FocusEnabled = FocusEnabled;
        settings.ControlledDisplayIds = ControlledDisplayIds?.ToList();
        return changed;
    }
}

public sealed record DisplayTarget(int Brightness, int? Kelvin);
public sealed record PlanPoint(int MinuteOfDay, string SceneId, int TransitionMinutes);

public static class RatioBinding
{
    public static Dictionary<string, int> CalculateAvailable(IReadOnlyDictionary<string, double> weights,
        IEnumerable<string> available, string anchor, int value)
    {
        var active = available.Where(weights.ContainsKey).ToDictionary(id => id, id => weights[id]);
        return active.ContainsKey(anchor) ? Calculate(active, anchor, value)
            : new Dictionary<string, int> { [anchor] = Math.Clamp(value, 0, 100) };
    }

    public static Dictionary<string, int> Calculate(IReadOnlyDictionary<string, double> weights, string anchor, int value)
    {
        if (!weights.TryGetValue(anchor, out var weight) || weight <= 0 || weights.Values.Any(x => x <= 0 || !double.IsFinite(x)))
            throw new ArgumentException("绑定前请为每块屏幕设置非零基准亮度。");
        var factor = Math.Min(Math.Clamp(value, 0, 100) / weight, 100 / weights.Values.Max());
        return weights.ToDictionary(x => x.Key, x => (int)Math.Round(x.Value * factor));
    }
}

public readonly record struct PlanResult(DateTime StartsAt, DateTime NextAt, Scene Scene, Scene Prior, double Progress)
{
    public bool TryGetTarget(string id, out int brightness, out int? kelvin,
        IReadOnlyDictionary<string, DisplayTarget>? origins = null)
    {
        brightness = 0;
        kelvin = null;
        if (!Scene.Targets.TryGetValue(id, out var target)) return false;
        var from = origins?.GetValueOrDefault(id) ?? Prior.Targets.GetValueOrDefault(id, target);
        brightness = (int)Math.Round(from.Brightness + (target.Brightness - from.Brightness) * Progress);
        // Native white is the baseline of our hardware warmth approximation. Restore the
        // actual original preset at a native endpoint, rather than retaining a 6500 K override.
        if (Progress <= 0) kelvin = from.Kelvin;
        else if (Progress >= 1) kelvin = target.Kelvin;
        else if (from.Kelvin.HasValue || target.Kelvin.HasValue)
            kelvin = (int)Math.Round((from.Kelvin ?? 6500) + ((target.Kelvin ?? 6500) - (from.Kelvin ?? 6500)) * Progress);
        return true;
    }
}

public static class DailyPlan
{
    public static bool HasSameDefinition(ControlSettings first, ControlSettings second)
    {
        if (first.ScheduleEnabled != second.ScheduleEnabled ||
            !first.Schedule.OrderBy(p => p.MinuteOfDay).SequenceEqual(second.Schedule.OrderBy(p => p.MinuteOfDay))) return false;
        foreach (var id in first.Schedule.Select(p => p.SceneId).Distinct())
        {
            var a = first.Scenes.FirstOrDefault(s => s.Id == id);
            var b = second.Scenes.FirstOrDefault(s => s.Id == id);
            if (a == null || b == null || a.Targets.Count != b.Targets.Count ||
                a.Targets.Any(p => !b.Targets.TryGetValue(p.Key, out var value) || p.Value != value)) return false;
            var left = a.Behavior ?? SceneBehavior.Capture(first);
            var right = b.Behavior ?? SceneBehavior.Capture(second);
            if (left.Linked != right.Linked || left.FocusEnabled != right.FocusEnabled ||
                left.LinkWeights.Count != right.LinkWeights.Count ||
                left.LinkWeights.Any(p => !right.LinkWeights.TryGetValue(p.Key, out var value) || p.Value != value) ||
                (left.ControlledDisplayIds == null ? right.ControlledDisplayIds != null :
                    right.ControlledDisplayIds == null || !left.ControlledDisplayIds.ToHashSet().SetEquals(right.ControlledDisplayIds))) return false;
        }
        return true;
    }

    public static void Validate(ControlSettings settings)
    {
        if (settings.SchemaVersion != 1) throw new ArgumentException("配置版本不受支持。");
        if (settings.Language is not ("system" or "zh-CN" or "en")) throw new ArgumentException("不支持的语言设置。");
        if (settings.FocusDelaySeconds is < 0 or > 600 || settings.FocusBrightness is < 0 or > 100)
            throw new ArgumentException("专注参数超出范围。");
        ValidateBinding(settings.Linked, settings.LinkWeights);
        if (settings.Scenes.Select(x => x.Id).Distinct().Count() != settings.Scenes.Count)
            throw new ArgumentException("场景标识重复。");
        foreach (var scene in settings.Scenes)
        {
            if (string.IsNullOrWhiteSpace(scene.Name)) throw new ArgumentException("请为场景命名。");
            if (scene.Behavior != null) ValidateBinding(scene.Behavior.Linked, scene.Behavior.LinkWeights);
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

    private static void ValidateBinding(bool linked, Dictionary<string, double> weights)
    {
        if (weights.Values.Any(value => value <= 0 || !double.IsFinite(value)) || (linked && weights.Count < 2))
            throw new ArgumentException("比例绑定需要至少两块屏幕，且各屏权重必须大于零。");
    }

    public static PlanResult? Evaluate(ControlSettings settings, DateTime now)
    {
        if (!settings.ScheduleEnabled || settings.Schedule.Count == 0) return null;
        // Only inspect neighbouring nodes; never expand days or retain animation frames.
        PlanPoint? current = null;
        var startsAt = DateTime.MinValue;
        var nextAt = DateTime.MaxValue;
        foreach (var point in settings.Schedule)
        {
            var at = now.Date.AddMinutes(point.MinuteOfDay);
            var previousAt = at <= now ? at : at.AddDays(-1);
            var followingAt = at > now ? at : at.AddDays(1);
            if (previousAt > startsAt) { startsAt = previousAt; current = point; }
            if (followingAt < nextAt) nextAt = followingAt;
        }
        PlanPoint? previous = null;
        var priorAt = DateTime.MinValue;
        foreach (var point in settings.Schedule)
        {
            var at = startsAt.Date.AddMinutes(point.MinuteOfDay);
            if (at >= startsAt) at = at.AddDays(-1);
            if (at > priorAt) { priorAt = at; previous = point; }
        }
        Scene? scene = null, prior = null;
        foreach (var candidate in settings.Scenes)
        {
            if (candidate.Id == current!.SceneId) scene = candidate;
            if (candidate.Id == previous!.SceneId) prior = candidate;
        }
        var duration = current!.TransitionMinutes;
        var progress = duration == 0 ? 1 : Math.Clamp((now - startsAt).TotalMinutes / duration, 0, 1);
        progress = progress * progress * (3 - 2 * progress);
        return new PlanResult(startsAt, nextAt, scene!, prior!, progress);
    }
}
