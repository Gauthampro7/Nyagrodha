namespace VedicCharts.Core;

/// <summary>
/// Uses Swiss Ephemeris (SwissEphNet) with selectable ayanamsa to compute sidereal positions.
/// </summary>
internal static class SwissEphCalculator
{
    private const int SE_SUN = 0, SE_MOON = 1, SE_MERCURY = 2, SE_VENUS = 3, SE_MARS = 4, SE_JUPITER = 5, SE_SATURN = 6;
    private const int SE_TRUE_NODE = 11;
    private const int SEFLG_SIDEREAL = 64 * 1024; // Swiss Ephemeris: sidereal positions (must match sweph)
    private const int SE_SIDM_LAHIRI = 1, SE_SIDM_RAMAN = 3, SE_SIDM_KRISHNAMURTI = 5, SE_SIDM_FAGAN_BRADLEY = 0, SE_SIDM_YUKTESHWAR = 7;

    public static int? GetSidMode(string? ayanamsaId)
    {
        if (string.IsNullOrWhiteSpace(ayanamsaId)) return SE_SIDM_LAHIRI;
        return ayanamsaId.Trim().ToUpperInvariant() switch
        {
            "LAHIRI" => SE_SIDM_LAHIRI,
            "RAMAN" => SE_SIDM_RAMAN,
            "KRISHNAMURTI" => SE_SIDM_KRISHNAMURTI,
            "FAGANBRADLEY" => SE_SIDM_FAGAN_BRADLEY,
            "YUKTESHWAR" => SE_SIDM_YUKTESHWAR,
            _ => null
        };
    }

    public static IReadOnlyList<BirthChartEntry>? Calculate(
        DateOnly birthDate,
        (int H, int M, int S) birthTime,
        double latitude,
        double longitude,
        double timeZoneOffsetHours,
        string? ayanamsaId)
    {
        int? sidMode = GetSidMode(ayanamsaId);
        if (!sidMode.HasValue) return null;

        double jdUt = LocalToJulianUt(birthDate, birthTime.H, birthTime.M, birthTime.S, timeZoneOffsetHours);
        var swissEph = new SwissEphNet.SwissEph();

        try
        {
            swissEph.swe_set_sid_mode(sidMode.Value, 0, 0);
        }
        catch
        {
            return null;
        }

        int iflag = SEFLG_SIDEREAL;
        var result = new List<BirthChartEntry>();
        var xx = new double[6];
        string serr = "";

        var cusps = new double[13];
        var ascmc = new double[10];
        int hret = swissEph.swe_houses_ex(jdUt, iflag, latitude, longitude, 'P', cusps, ascmc);
        if (hret < 0) return null;
        result.Add(MakeEntry("Lagna", cusps[1]));

        int[] planets = { SE_SUN, SE_MOON, SE_MARS, SE_MERCURY, SE_JUPITER, SE_VENUS, SE_SATURN };
        string[] names = { "Sun", "Moon", "Mars", "Mercury", "Jupiter", "Venus", "Saturn" };
        for (int i = 0; i < planets.Length; i++)
        {
            if (swissEph.swe_calc_ut(jdUt, planets[i], iflag, xx, ref serr) < 0) return null;
            result.Add(MakeEntry(names[i], xx[0]));
        }

        if (swissEph.swe_calc_ut(jdUt, SE_TRUE_NODE, iflag, xx, ref serr) < 0) return null;
        double rahuLon = xx[0];
        result.Add(MakeEntry("Rahu", rahuLon));
        double ketuLon = (rahuLon + 180) % 360;
        if (ketuLon < 0) ketuLon += 360;
        result.Add(MakeEntry("Ketu", ketuLon));

        return result;
    }

    public sealed record RawSiderealChart(
        double LagnaLongitude,
        IReadOnlyDictionary<string, double> BodyLongitudes);

    public static RawSiderealChart? CalculateRaw(
        DateOnly birthDate,
        (int H, int M, int S) birthTime,
        double latitude,
        double longitude,
        double timeZoneOffsetHours,
        string? ayanamsaId)
    {
        int? sidMode = GetSidMode(ayanamsaId);
        if (!sidMode.HasValue) return null;

        double jdUt = LocalToJulianUt(birthDate, birthTime.H, birthTime.M, birthTime.S, timeZoneOffsetHours);
        var swissEph = new SwissEphNet.SwissEph();

        try
        {
            swissEph.swe_set_sid_mode(sidMode.Value, 0, 0);
        }
        catch
        {
            return null;
        }

        int iflag = SEFLG_SIDEREAL;
        var xx = new double[6];
        string serr = "";

        var cusps = new double[13];
        var ascmc = new double[10];
        int hret = swissEph.swe_houses_ex(jdUt, iflag, latitude, longitude, 'P', cusps, ascmc);
        if (hret < 0) return null;

        var dict = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        int[] planets = { SE_SUN, SE_MOON, SE_MARS, SE_MERCURY, SE_JUPITER, SE_VENUS, SE_SATURN };
        string[] names = { "Sun", "Moon", "Mars", "Mercury", "Jupiter", "Venus", "Saturn" };
        for (int i = 0; i < planets.Length; i++)
        {
            if (swissEph.swe_calc_ut(jdUt, planets[i], iflag, xx, ref serr) < 0) return null;
            dict[names[i]] = xx[0];
        }

        if (swissEph.swe_calc_ut(jdUt, SE_TRUE_NODE, iflag, xx, ref serr) < 0) return null;
        double rahuLon = xx[0];
        dict["Rahu"] = rahuLon;
        double ketuLon = (rahuLon + 180) % 360;
        if (ketuLon < 0) ketuLon += 360;
        dict["Ketu"] = ketuLon;

        return new RawSiderealChart(
            LagnaLongitude: cusps[1],
            BodyLongitudes: dict);
    }

    private static double LocalToJulianUt(DateOnly d, int h, int min, int sec, double offsetHours)
    {
        double totalHours = h + min / 60.0 + sec / 3600.0 - offsetHours;
        int day = d.Day, month = d.Month, year = d.Year;
        while (totalHours < 0) { totalHours += 24; day--; }
        while (totalHours >= 24) { totalHours -= 24; day++; }
        if (day < 1) { month--; if (month < 1) { month = 12; year--; } day += DateTime.DaysInMonth(year, month); }
        double jd = DateToJulianDay(year, month, day);
        return jd + totalHours / 24.0;
    }

    private static double DateToJulianDay(int y, int m, int d)
    {
        if (m <= 2) { y--; m += 12; }
        int A = y / 100;
        int B = 2 - A + (A / 4);
        return Math.Floor(365.25 * (y + 4716)) + Math.Floor(30.6001 * (m + 1)) + d + B - 1524.5;
    }

    private static BirthChartEntry MakeEntry(string bodyName, double longitude)
    {
        var (signName, degreeInSign) = ZodiacHelper.LongitudeToZodiac(longitude);
        return new BirthChartEntry(bodyName, signName, degreeInSign);
    }
}
