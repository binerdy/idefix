using Idefix.Core.Sky;

namespace Idefix.ApiTests;

public sealed class SkyTests
{
    [Theory]
    [InlineData("06:00", SkyPhase.Day)]
    [InlineData("17:59", SkyPhase.Day)]
    [InlineData("18:00", SkyPhase.Evening)]
    [InlineData("21:59", SkyPhase.Evening)]
    [InlineData("22:00", SkyPhase.Night)]
    [InlineData("05:59", SkyPhase.Night)]
    public void PhaseFollowsTheTimeArc(string time, SkyPhase expected)
    {
        Assert.Equal(expected, SkyClock.Phase(TimeOnly.Parse(time, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void SunPeaksAtNoonAndIsAbsentAtNight()
    {
        Assert.Equal(0.5, SkyClock.SunProgress(new TimeOnly(12, 0)));
        Assert.Null(SkyClock.SunProgress(new TimeOnly(23, 0)));
    }

    [Fact]
    public void MoonIsVisibleOnlyFromEveningToMorning()
    {
        Assert.Equal(0, SkyClock.MoonProgress(new TimeOnly(18, 0)));
        Assert.Equal(0.5, SkyClock.MoonProgress(new TimeOnly(0, 0)));
        Assert.Null(SkyClock.MoonProgress(new TimeOnly(9, 0)));
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

    private static double Distance(SkyPoint a, SkyPoint b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
}
