namespace Idefix.Core.Sky;

public sealed record Star(double X, double Y, double Radius, double Phase, double PeriodSeconds)
{
    public double Brightness(double seconds) =>
        0.22 + (0.12 * Math.Sin((2 * Math.PI * seconds / this.PeriodSeconds) + this.Phase));
}
