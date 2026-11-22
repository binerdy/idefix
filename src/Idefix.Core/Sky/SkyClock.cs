namespace Idefix.Core.Sky;

public static class SkyClock
{
    private static readonly TimeSpan DayStart = TimeSpan.FromHours(6);
    private static readonly TimeSpan EveningStart = TimeSpan.FromHours(18);
    private static readonly TimeSpan NightStart = TimeSpan.FromHours(22);
    private static readonly TimeSpan HalfDay = TimeSpan.FromHours(12);

    public static SkyPhase Phase(TimeOnly time)
    {
        var t = time.ToTimeSpan();
        if (t >= DayStart && t < EveningStart)
        {
            return SkyPhase.Day;
        }

        return t >= EveningStart && t < NightStart ? SkyPhase.Evening : SkyPhase.Night;
    }

    public static double? SunProgress(TimeOnly time)
    {
        var t = time.ToTimeSpan();
        return t >= DayStart && t < EveningStart ? (t - DayStart) / HalfDay : null;
    }

    public static double? MoonProgress(TimeOnly time)
    {
        if (SunProgress(time) is not null)
        {
            return null;
        }

        var sinceEvening = time.ToTimeSpan() - EveningStart;
        if (sinceEvening < TimeSpan.Zero)
        {
            sinceEvening += TimeSpan.FromHours(24);
        }

        return sinceEvening / HalfDay;
    }

    public static SkyPoint ArcPoint(double progress, double width, double height)
    {
        var p = Math.Clamp(progress, 0, 1);
        var x = width * (0.08 + (0.84 * p));
        var y = height * (0.62 - (0.48 * Math.Sin(Math.PI * p)));
        return new SkyPoint(x, y);
    }
}
