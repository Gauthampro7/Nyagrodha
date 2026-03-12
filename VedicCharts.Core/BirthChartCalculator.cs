namespace VedicCharts.Core;

/// <summary>
/// Computes Vedic birth chart (planets and Lagna in zodiac) using VedAstro.Library.
/// </summary>
public static class BirthChartCalculator
{
    /// <summary>
    /// Computes the birth chart for the given local date/time and location.
    /// </summary>
    /// <param name="birthDate">Birth date (local).</param>
    /// <param name="birthTime">Birth time (hours, minutes, optional seconds) in local time.</param>
    /// <param name="latitude">Birth place latitude (degrees).</param>
    /// <param name="longitude">Birth place longitude (degrees).</param>
    /// <param name="timeZoneOffsetHours">Offset from UTC in hours (e.g. 5.5 for IST). Can use fractional.</param>
    /// <param name="ayanamsaId">Ayanamsa identifier (e.g. Lahiri, Raman). VedAstro.Library 1.2.0 may use a fixed ayanamsa; this is for future use.</param>
    /// <returns>Ordered list: Lagna, then Sun, Moon, Mars, Mercury, Jupiter, Venus, Saturn, Rahu, Ketu.</returns>
    public static IReadOnlyList<BirthChartEntry> Calculate(
        DateOnly birthDate,
        (int Hours, int Minutes, int Seconds) birthTime,
        double latitude,
        double longitude,
        double timeZoneOffsetHours,
        string? ayanamsaId = null)
    {
        // When a supported ayanamsa is selected, use Swiss Ephemeris so the selection actually changes the result.
        var swissResult = SwissEphCalculator.Calculate(birthDate, (birthTime.Hours, birthTime.Minutes, birthTime.Seconds), latitude, longitude, timeZoneOffsetHours, ayanamsaId);
        if (swissResult != null)
            return swissResult;

        // Fallback: VedAstro (uses its default ayanamsa, typically Lahiri)
        string offsetStr = FormatTimeZoneOffset(timeZoneOffsetHours);
        string timeStr = $"{birthTime.Hours:D2}:{birthTime.Minutes:D2} {birthDate.Day:D2}/{birthDate.Month:D2}/{birthDate.Year} {offsetStr}";

        // VedAstro GeoLocation is (name, LONGITUDE, latitude) - order matters
        var geoLocation = new VedAstro.Library.GeoLocation("", longitude, latitude);
        var time = new VedAstro.Library.Time(timeStr, geoLocation);

        var result = new List<BirthChartEntry>();

        var house1 = VedAstro.Library.AstronomicalCalculator.GetHouse(VedAstro.Library.HouseName.House1, time);
        double lagnaLongitude = house1.GetMiddleLongitude().TotalDegrees;
        result.Add(MakeEntry("Lagna", lagnaLongitude));

        var planetList = VedAstro.Library.AstronomicalCalculator.GetAllPlanetLongitude(time);
        var order = new[] { VedAstro.Library.PlanetName.Sun, VedAstro.Library.PlanetName.Moon, VedAstro.Library.PlanetName.Mars,
            VedAstro.Library.PlanetName.Mercury, VedAstro.Library.PlanetName.Jupiter, VedAstro.Library.PlanetName.Venus,
            VedAstro.Library.PlanetName.Saturn, VedAstro.Library.PlanetName.Rahu, VedAstro.Library.PlanetName.Ketu };
        var nameMap = new Dictionary<VedAstro.Library.PlanetName, string>
        {
            [VedAstro.Library.PlanetName.Sun] = "Sun",
            [VedAstro.Library.PlanetName.Moon] = "Moon",
            [VedAstro.Library.PlanetName.Mars] = "Mars",
            [VedAstro.Library.PlanetName.Mercury] = "Mercury",
            [VedAstro.Library.PlanetName.Jupiter] = "Jupiter",
            [VedAstro.Library.PlanetName.Venus] = "Venus",
            [VedAstro.Library.PlanetName.Saturn] = "Saturn",
            [VedAstro.Library.PlanetName.Rahu] = "Rahu",
            [VedAstro.Library.PlanetName.Ketu] = "Ketu",
        };
        var byPlanet = planetList.ToDictionary(p => p.GetPlanetName(), p => p.GetPlanetLongitude().TotalDegrees);
        foreach (var planet in order)
            if (byPlanet.TryGetValue(planet, out double lon))
                result.Add(MakeEntry(nameMap[planet], lon));

        return result;
    }

    private static string FormatTimeZoneOffset(double timeZoneOffsetHours)
    {
        // Handle fractional hours (e.g. 5.5 = IST +05:30, -5.5 = -05:30)
        int hours = (int)timeZoneOffsetHours;
        double frac = timeZoneOffsetHours - hours;
        if (frac < 0) frac += 1; // e.g. -5.5 -> hours=-5, frac=0.5
        int minutes = (int)Math.Round(frac * 60);
        if (minutes >= 60) { minutes = 0; hours += 1; }
        if (minutes < 0) minutes = 0;
        return timeZoneOffsetHours >= 0
            ? $"+{hours:D2}:{minutes:D2}"
            : $"-{Math.Abs(hours):D2}:{minutes:D2}";
    }

    private static BirthChartEntry MakeEntry(string bodyName, double longitude)
    {
        var (signName, degreeInSign) = ZodiacHelper.LongitudeToZodiac(longitude);
        return new BirthChartEntry(bodyName, signName, degreeInSign);
    }
}
