using MonitorsFocus.Core;

internal static class DailyPlanChecks
{
    private static ControlSettings Settings() => new()
    {
        ScheduleEnabled = true,
        Scenes =
        [
            new Scene { Id = "day", Targets = new() { ["screen"] = new(90, null) } },
            new Scene { Id = "night", Targets = new() { ["screen"] = new(10, 2700) } }
        ],
        // Unsorted input is valid; the six-hour night fade crosses midnight.
        Schedule = [new(20 * 60, "night", 360), new(8 * 60, "day", 120)]
    };

    public static Task LongFadeAsync()
    {
        var settings = Settings();
        DailyPlan.Validate(settings);
        var start = new DateTime(2026, 11, 1, 20, 0, 0);
        var expected = new[] { (0, 90, (int?)null), (90, 78, (int?)5906),
            (180, 50, (int?)4600), (270, 22, (int?)3294), (360, 10, (int?)2700) };
        foreach (var (minutes, brightness, kelvin) in expected)
        {
            var plan = DailyPlan.Evaluate(settings, start.AddMinutes(minutes))!.Value;
            Check(plan.StartsAt == start && plan.NextAt == start.Date.AddDays(1).AddHours(8));
            Check(plan.TryGetTarget("screen", out var b, out var k) && b == brightness && k == kelvin);
            Check(!plan.TryGetTarget("missing", out _, out _));
        }
        var morning = start.Date.AddDays(1).AddHours(8);
        Check(DailyPlan.Evaluate(settings, morning.AddHours(1))!.Value.TryGetTarget("screen", out var mid, out var warm));
        Check(mid == 50 && warm == 4600);
        Check(DailyPlan.Evaluate(settings, morning.AddHours(2))!.Value.TryGetTarget("screen", out var end, out var native));
        Check(end == 90 && native == null);
        return Task.CompletedTask;
    }

    public static Task BoundariesAsync()
    {
        var settings = Settings();
        settings.Schedule[0] = settings.Schedule[0] with { TransitionMinutes = 0 };
        var at = new DateTime(2026, 11, 1, 20, 0, 0);
        Check(DailyPlan.Evaluate(settings, at)!.Value.TryGetTarget("screen", out var b, out var k));
        Check(b == 10 && k == 2700);
        settings.Schedule.RemoveAt(1);
        settings.Schedule[0] = settings.Schedule[0] with { TransitionMinutes = 1440 };
        DailyPlan.Validate(settings);
        var only = DailyPlan.Evaluate(settings, at.AddHours(10))!.Value;
        Check(only.NextAt == at.AddDays(1));
        Check(only.TryGetTarget("screen", out b, out k) && b == 10 && k == 2700);
        settings.ScheduleEnabled = false;
        Check(DailyPlan.Evaluate(settings, at) == null);
        settings.ScheduleEnabled = true;
        settings.Schedule.Clear();
        Check(DailyPlan.Evaluate(settings, at) == null);
        settings = Settings();
        settings.Schedule[0] = settings.Schedule[0] with { TransitionMinutes = 721 };
        try { DailyPlan.Validate(settings); throw new Exception("Expected overlapping fade rejection."); }
        catch (ArgumentException) { }
        return Task.CompletedTask;
    }

    public static Task AllocationAsync()
    {
        var settings = Settings();
        var start = new DateTime(2026, 11, 1);
        for (var i = 0; i < 1000; i++)
            DailyPlan.Evaluate(settings, start.AddSeconds(i))!.Value.TryGetTarget("screen", out _, out _);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var checksum = 0L;
        for (var second = 0; second < 3 * 86400; second++)
        {
            var plan = DailyPlan.Evaluate(settings, start.AddSeconds(second))!.Value;
            if (!plan.TryGetTarget("screen", out var brightness, out var kelvin) || brightness is < 10 or > 90 ||
                kelvin is < 2700 or > 6500) throw new Exception("Invalid long-running fade target.");
            checksum += brightness;
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(checksum > 0 && allocated <= 1024);
        Console.WriteLine($"  Simulated 72 hours at 1 Hz: {allocated} bytes allocated by plan evaluation.");
        return Task.CompletedTask;
    }

    private static void Check(bool condition)
    {
        if (!condition) throw new Exception("Daily plan assertion failed.");
    }
}
