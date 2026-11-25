using System.Globalization;
using Idefix.Core.Sky;

namespace Idefix.ApiTests;

public sealed class SkyTests
{
    private static readonly TimeZoneInfo Zurich = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
    private static readonly SkyClock ZurichClock = new(SkyLocation.Zurich, Zurich);

    [Theory]
    [InlineData(2026, 6, 21, "05:30", "21:26")]
    [InlineData(2026, 12, 21, "08:12", "16:37")]
    public void SunriseAndSunsetMatchTheAlmanac(int year, int month, int day, string sunrise, string sunset)
    {
        var sun = ZurichClock.SunOn(new DateOnly(year, month, day));

        AssertNear(TimeOnly.Parse(sunrise, CultureInfo.InvariantCulture), sun.Sunrise);
        AssertNear(TimeOnly.Parse(sunset, CultureInfo.InvariantCulture), sun.Sunset);
    }

    [Fact]
    public void SummerTimeShiftsTheLocalSunriseByAnHour()
    {
        var before = ZurichClock.SunOn(new DateOnly(2026, 3, 28)).Sunrise;
        var after = ZurichClock.SunOn(new DateOnly(2026, 3, 29)).Sunrise;

        Assert.InRange(after.TimeOfDay - before.TimeOfDay, TimeSpan.FromMinutes(55), TimeSpan.FromMinutes(59));
        Assert.Equal(TimeSpan.FromHours(2), after.Offset);
    }

    [Theory]
    [InlineData(2026, 12, 21, 17, false)]
    [InlineData(2026, 6, 21, 17, true)]
    [InlineData(2026, 6, 21, 22, false)]
    [InlineData(2026, 12, 21, 12, true)]
    public void DaylightDecidesTheTheme(int year, int month, int day, int hour, bool isDay)
    {
        Assert.Equal(isDay, ZurichClock.IsDay(Local(year, month, day, hour)));
    }

    [Fact]
    public void SunPeaksAroundNoonAndIsAbsentAtNight()
    {
        Assert.InRange(ZurichClock.SunProgress(Local(2026, 12, 21, 12, 28))!.Value, 0.45, 0.55);
        Assert.Null(ZurichClock.SunProgress(Local(2026, 12, 21, 23)));
    }

    [Fact]
    public void MoonCrossesTheWholeNight()
    {
        Assert.Null(ZurichClock.MoonProgress(Local(2026, 12, 21, 12)));
        Assert.InRange(ZurichClock.MoonProgress(Local(2026, 12, 21, 16, 50))!.Value, 0, 0.05);
        Assert.InRange(ZurichClock.MoonProgress(Local(2026, 12, 22, 0, 28))!.Value, 0.45, 0.55);
        Assert.InRange(ZurichClock.MoonProgress(Local(2026, 12, 22, 8))!.Value, 0.95, 1);
    }

    [Fact]
    public void PolarSummerAndWinterNeverFlipTheTheme()
    {
        var tromso = new SkyClock(new SkyLocation(69.65, 18.96), Zurich);

        Assert.True(tromso.IsDay(Local(2026, 6, 21, 1)));
        Assert.False(tromso.IsDay(Local(2026, 12, 21, 12)));
    }

    [Fact]
    public void UnknownTimeZonesUseTheirOffsetAsLongitude()
    {
        var tokyo = TimeZoneInfo.CreateCustomTimeZone("Test/Tokyo", TimeSpan.FromHours(9), "Tokyo", "Tokyo");

        Assert.Equal(SkyLocation.Zurich, SkyLocation.ForTimeZone(Zurich));
        Assert.Equal(135, SkyLocation.ForTimeZone(tokyo).Longitude);
    }

    [Fact]
    public void ArcIsHighestInTheMiddle()
    {
        var rise = SkyClock.ArcPoint(0, 1000, 800);
        var zenith = SkyClock.ArcPoint(0.5, 1000, 800);
        var set = SkyClock.ArcPoint(1, 1000, 800);

        Assert.True(zenith.Y < rise.Y);
        Assert.Equal(rise.Y, set.Y, precision: 6);
        Assert.True(rise.X < zenith.X && zenith.X < set.X);
    }

    [Fact]
    public void ShootingStarsAppearEvery25To60Seconds()
    {
        var random = new Random(7);
        var delays = Enumerable.Range(0, 200).Select(_ => ShootingStarPlanner.NextDelay(random)).ToList();

        Assert.All(delays, d => Assert.InRange(d, TimeSpan.FromSeconds(25), TimeSpan.FromSeconds(60)));
    }

    [Fact]
    public void ShootingStarTravelsTowardsTheLastPointerPosition()
    {
        var pointer = new SkyPoint(0, 900);

        var star = ShootingStarPlanner.Plan(new Random(3), 1200, 900, pointer);
        var before = Distance(star.Head(0), pointer);
        var after = Distance(star.Head(1), pointer);

        Assert.True(after < before);
    }

    [Fact]
    public void StarsAreDimAndDeterministic()
    {
        var first = StarField.Generate(42, 30);
        var second = StarField.Generate(42, 30);

        Assert.Equal(first, second);
        Assert.All(first, s => Assert.InRange(s.Brightness(3.3), 0.05, 0.4));
    }

    private static DateTimeOffset Local(int year, int month, int day, int hour, int minute = 0)
    {
        var wall = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(wall, Zurich.GetUtcOffset(wall));
    }

    private static void AssertNear(TimeOnly expected, DateTimeOffset actual)
    {
        var difference = (actual.TimeOfDay - expected.ToTimeSpan()).Duration();
        Assert.True(difference <= TimeSpan.FromMinutes(3), $"Expected about {expected}, got {actual:HH:mm}.");
    }

    private static double Distance(SkyPoint a, SkyPoint b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
}
