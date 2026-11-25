namespace Idefix.Core.Sky;

public sealed record SunTimes(DateTimeOffset Sunrise, DateTimeOffset Sunset)
{
    private const double J2000 = 2451545.0;
    private const double UnixEpochJulianDay = 2440587.5;
    private const double AxialTilt = 23.4397;
    private const double HorizonAltitude = -0.833;

    public static SunTimes Calculate(DateOnly date, SkyLocation location, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(zone);

        var noon = new DateTimeOffset(date.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);
        var day = Math.Round(ToJulianDay(noon) - J2000 + 0.0008);
        var meanNoon = day - (location.Longitude / 360);
        var anomaly = Normalize(357.5291 + (0.98560028 * meanNoon));
        var m = Radians(anomaly);
        var center = (1.9148 * Math.Sin(m)) + (0.02 * Math.Sin(2 * m)) + (0.0003 * Math.Sin(3 * m));
        var eclipticLongitude = Radians(Normalize(anomaly + center + 180 + 102.9372));
        var transit = J2000 + meanNoon + (0.0053 * Math.Sin(m)) - (0.0069 * Math.Sin(2 * eclipticLongitude));
        var declination = Math.Asin(Math.Sin(eclipticLongitude) * Math.Sin(Radians(AxialTilt)));
        var latitude = Radians(location.Latitude);
        var cosHourAngle = (Math.Sin(Radians(HorizonAltitude)) - (Math.Sin(latitude) * Math.Sin(declination)))
            / (Math.Cos(latitude) * Math.Cos(declination));

        // Beyond ±1 the sun never sets (midnight sun) or never rises (polar night).
        var hourAngle = Math.Acos(Math.Clamp(cosHourAngle, -1, 1)) * 180 / Math.PI;
        var sunrise = FromJulianDay(transit - (hourAngle / 360));
        var sunset = FromJulianDay(transit + (hourAngle / 360));
        return new SunTimes(TimeZoneInfo.ConvertTime(sunrise, zone), TimeZoneInfo.ConvertTime(sunset, zone));
    }

    public bool IsDay(DateTimeOffset now) => now >= this.Sunrise && now < this.Sunset;

    private static double ToJulianDay(DateTimeOffset moment) =>
        (moment.ToUnixTimeMilliseconds() / 86_400_000.0) + UnixEpochJulianDay;

    private static DateTimeOffset FromJulianDay(double julianDay) =>
        DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Round((julianDay - UnixEpochJulianDay) * 86_400_000.0));

    private static double Normalize(double degrees) => ((degrees % 360) + 360) % 360;

    private static double Radians(double degrees) => degrees * Math.PI / 180;
}
