namespace VedicCharts.Core;

/// <summary>
/// A place (city/location) with coordinates and IANA timezone for birth chart calculations.
/// </summary>
public sealed record Place(
    long Id,
    string Name,
    string Country,
    double Latitude,
    double Longitude,
    string TimeZone);
