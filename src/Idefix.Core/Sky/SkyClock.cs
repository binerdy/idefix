namespace Idefix.Core.Sky;

public sealed class SkyClock(SkyLocation location, TimeZoneInfo zone)
{
    public static SkyClock Local { get; } = new(SkyLocation.ForTimeZone(TimeZoneInfo.Local), TimeZoneInfo.Local);

    public static SkyPoint ArcPoint(double progress, double width, double height)
    {
        var p = Math.Clamp(progress, 0, 1);
        var x = width * (0.08 + (0.84 * p));
        var y = height * (0.62 - (0.48 * Math.Sin(Math.PI * p)));
        return new SkyPoint(x, y);
    }

    public SunTimes SunOn(DateOnly date) => SunTimes.Calculate(date, location, zone);

    public bool IsDay(DateTimeOffset now) => this.SunOn(this.LocalDate(now)).IsDay(now);

    public double? SunProgress(DateTimeOffset now)
    {
        var sun = this.SunOn(this.LocalDate(now));
        return sun.IsDay(now) ? (now - sun.Sunrise) / (sun.Sunset - sun.Sunrise) : null;
    }

    public double? MoonProgress(DateTimeOffset now)
    {
        var today = this.LocalDate(now);
        var sun = this.SunOn(today);
        if (sun.IsDay(now))
        {
            return null;
        }

        var (dusk, dawn) = now >= sun.Sunset
            ? (sun.Sunset, this.SunOn(today.AddDays(1)).Sunrise)
            : (this.SunOn(today.AddDays(-1)).Sunset, sun.Sunrise);
        return (now - dusk) / (dawn - dusk);
    }

    private DateOnly LocalDate(DateTimeOffset now) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
}
