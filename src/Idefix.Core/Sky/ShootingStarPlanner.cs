using System.Diagnostics.CodeAnalysis;

namespace Idefix.Core.Sky;

public static class ShootingStarPlanner
{
    public static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(25);
    public static readonly TimeSpan MaxInterval = TimeSpan.FromSeconds(60);

    [SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Decorative timing.")]
    public static TimeSpan NextDelay(Random random) =>
        MinInterval + ((MaxInterval - MinInterval) * random.NextDouble());

    [SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Decorative placement.")]
    public static ShootingStar Plan(Random random, double width, double height, SkyPoint? lastPointer)
    {
        var start = new SkyPoint(width * (0.2 + (0.6 * random.NextDouble())), height * (0.08 + (0.3 * random.NextDouble())));
        var target = lastPointer ?? new SkyPoint(width, height);
        var dx = target.X - start.X;
        var dy = target.Y - start.Y;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        var direction = distance < 1 ? new SkyPoint(Math.Sqrt(0.5), Math.Sqrt(0.5)) : new SkyPoint(dx / distance, dy / distance);
        var length = Math.Min(140 + (random.NextDouble() * 120), Math.Max(distance, 80));
        return new ShootingStar(start, direction, length, TimeSpan.FromMilliseconds(900));
    }
}
