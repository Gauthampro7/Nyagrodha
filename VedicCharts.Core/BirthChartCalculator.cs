namespace VedicCharts.Core;

/// <summary>
/// Computes Vedic birth chart (planets and Lagna in zodiac) using VedAstro.Library.
/// </summary>
public static class BirthChartCalculator
{
    private static readonly string[] DefaultBodyOrder =
    {
        "Sun","Moon","Mars","Mercury","Jupiter","Venus","Saturn","Rahu","Ketu"
    };

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
        if (swissResult != null) return swissResult;

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

    /// <summary>
    /// Computes a house-based chart data structure for rendering North/South style charts for a given divisional chart id.
    /// Houses are computed using whole-sign houses based on Lagna in the selected divisional chart.
    /// </summary>
    public static VedicChartData CalculateChartData(
        string chartTypeId,
        DateOnly birthDate,
        (int Hours, int Minutes, int Seconds) birthTime,
        double latitude,
        double longitude,
        double timeZoneOffsetHours,
        string? ayanamsaId = null)
    {
        // Prefer Swiss Ephemeris path so ayanamsa selection takes effect.
        var raw = SwissEphCalculator.CalculateRaw(birthDate, (birthTime.Hours, birthTime.Minutes, birthTime.Seconds),
            latitude, longitude, timeZoneOffsetHours, ayanamsaId);

        if (raw == null)
        {
            // Fallback: VedAstro (uses its default ayanamsa, typically Lahiri)
            string offsetStr = FormatTimeZoneOffset(timeZoneOffsetHours);
            string timeStr = $"{birthTime.Hours:D2}:{birthTime.Minutes:D2} {birthDate.Day:D2}/{birthDate.Month:D2}/{birthDate.Year} {offsetStr}";

            // VedAstro GeoLocation is (name, LONGITUDE, latitude) - order matters
            var geoLocation = new VedAstro.Library.GeoLocation("", longitude, latitude);
            var time = new VedAstro.Library.Time(timeStr, geoLocation);

            var planetList = VedAstro.Library.AstronomicalCalculator.GetAllPlanetLongitude(time);
            var dict = planetList.ToDictionary(p => p.GetPlanetName().ToString(), p => p.GetPlanetLongitude().TotalDegrees, StringComparer.OrdinalIgnoreCase);
            dict["Rahu"] = planetList.First(p => p.GetPlanetName() == VedAstro.Library.PlanetName.Rahu).GetPlanetLongitude().TotalDegrees;
            dict["Ketu"] = planetList.First(p => p.GetPlanetName() == VedAstro.Library.PlanetName.Ketu).GetPlanetLongitude().TotalDegrees;

            var house1 = VedAstro.Library.AstronomicalCalculator.GetHouse(VedAstro.Library.HouseName.House1, time);
            raw = new SwissEphCalculator.RawSiderealChart(house1.GetMiddleLongitude().TotalDegrees, dict);
        }

        // Compute divisional Lagna sign
        var lagnaDivSign = VargaHelper.LongitudeToDivisionalSign(raw.LagnaLongitude, chartTypeId);
        int lagnaIndex = VargaHelper.SignIndex(lagnaDivSign.SignName);

        // Compute divisional planet signs and degrees within sign
        var planetDivSigns = raw.BodyLongitudes
            .Where(kv => DefaultBodyOrder.Contains(kv.Key, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(
                kv => NormalizeBodyName(kv.Key),
                kv => VargaHelper.LongitudeToDivisionalSign(kv.Value, chartTypeId),
                StringComparer.OrdinalIgnoreCase);

        // Build whole-sign houses starting from divisional Lagna sign
        var houseBodies = new List<List<VedicChartBody>>(capacity: 12);
        var houseSigns = new List<string>(capacity: 12);
        for (int i = 0; i < 12; i++)
        {
            int signIdx = (lagnaIndex + i) % 12;
            string signName = VargaHelper.SignNameFromIndex(signIdx);
            houseSigns.Add(signName);
            houseBodies.Add(new List<VedicChartBody>());
        }

        // Assign bodies to houses by sign match
        foreach (var body in DefaultBodyOrder)
        {
            if (!planetDivSigns.TryGetValue(body, out var divSign)) continue;
            int idx = houseSigns.FindIndex(s => s.Equals(divSign.SignName, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) continue;
            houseBodies[idx].Add(new VedicChartBody(ShortBody(body), divSign.DegreeInSign));
        }

        // Ensure output lists are immutable
        var normalizedHouses = new List<VedicChartHouse>(capacity: 12);
        for (int i = 0; i < 12; i++)
            normalizedHouses.Add(new VedicChartHouse(i + 1, houseSigns[i], houseBodies[i].ToList()));

        return new VedicChartData(chartTypeId, normalizedHouses);
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

    private static string NormalizeBodyName(string body) => body.Trim() switch
    {
        "TrueNode" => "Rahu",
        _ => body.Trim()
    };

    private static string ShortBody(string body) => body switch
    {
        "Sun" => "Su",
        "Moon" => "Mo",
        "Mars" => "Ma",
        "Mercury" => "Me",
        "Jupiter" => "Ju",
        "Venus" => "Ve",
        "Saturn" => "Sa",
        "Rahu" => "Ra",
        "Ketu" => "Ke",
        _ => body
    };
}
