namespace Idefix.Core.Sky;

public sealed record ShootingStar(SkyPoint Start, SkyPoint Direction, double Length, TimeSpan Duration)
{
    public static double Opacity(double progress) => Math.Sin(Math.PI * Math.Clamp(progress, 0, 1)) * 0.55;

    public SkyPoint Head(double progress)
    {
        var travelled = this.Length * Math.Clamp(progress, 0, 1);
        return new SkyPoint(this.Start.X + (this.Direction.X * travelled), this.Start.Y + (this.Direction.Y * travelled));
    }
}
