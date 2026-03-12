namespace VedicCharts.Core;

/// <summary>
/// Converts IANA timezone (or longitude fallback) to UTC offset for VedAstro Time string.
/// </summary>
public static class TimeZoneHelper
{
    /// <summary>
    /// Gets the UTC offset in hours (e.g. 5.5 for IST) for the given IANA timezone and local date/time.
    /// Use the birth date/time so DST is correct for that moment.
    /// If the timezone ID is missing or invalid, returns null so caller can use longitude fallback.
    /// </summary>
    public static double? GetOffsetHoursFromTimeZone(string? ianaTimeZoneId, DateTime localDateTime)
    {
        if (string.IsNullOrWhiteSpace(ianaTimeZoneId)) return null;
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(ianaTimeZoneId.Trim());
            var offset = tz.GetUtcOffset(localDateTime);
            return offset.TotalHours;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets UTC offset in hours: uses IANA timezone if valid; otherwise estimates from longitude (longitude/15).
    /// </summary>
    public static double GetOffsetHours(string? ianaTimeZoneId, DateTime localDateTime, double longitude)
    {
        var fromTz = GetOffsetHoursFromTimeZone(ianaTimeZoneId, localDateTime);
        if (fromTz.HasValue)
            return fromTz.Value;
        return GetOffsetHoursFromLongitude(longitude);
    }

    /// <summary>
    /// Rough UTC offset in hours from longitude (e.g. 75°E -> +5, -75°W -> -5). One timezone hour ≈ 15°.
    /// </summary>
    public static double GetOffsetHoursFromLongitude(double longitude)
    {
        return Math.Clamp(longitude / 15.0, -12, 12);
    }
}
