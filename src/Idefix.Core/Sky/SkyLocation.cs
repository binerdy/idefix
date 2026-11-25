namespace Idefix.Core.Sky;

public sealed record SkyLocation(double Latitude, double Longitude)
{
    public static readonly SkyLocation Zurich = new(47.37, 8.54);

    private const double DefaultLatitude = 47;

    private static readonly string[] CentralEuropeanZones =
    [
        "W. Europe Standard Time",
        "Central Europe Standard Time",
        "Romance Standard Time",
        "Central European Standard Time",
        "Europe/Zurich",
        "Europe/Berlin",
        "Europe/Vienna",
        "Europe/Paris",
    ];

    // Idefix asks for no location permission; the time zone's base offset is a good enough longitude for the theme switch.
    public static SkyLocation ForTimeZone(TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        return CentralEuropeanZones.Contains(zone.Id, StringComparer.OrdinalIgnoreCase)
            ? Zurich
            : new SkyLocation(DefaultLatitude, zone.BaseUtcOffset.TotalHours * 15);
    }
}
