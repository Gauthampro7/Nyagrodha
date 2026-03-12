namespace VedicCharts.Core;

/// <summary>
/// Converts ecliptic longitude (0-360) to zodiac sign and degree-in-sign.
/// </summary>
public static class ZodiacHelper
{
    private static readonly string[] SignNames =
    {
        "Aries", "Taurus", "Gemini", "Cancer", "Leo", "Virgo",
        "Libra", "Scorpio", "Sagittarius", "Capricorn", "Aquarius", "Pisces"
    };

    /// <summary>
    /// Converts longitude in degrees (0-360) to sign name and degree within that sign (0-30).
    /// </summary>
    public static (string SignName, double DegreeInSign) LongitudeToZodiac(double longitude)
    {
        double normalized = ((longitude % 360) + 360) % 360;
        int signIndex = (int)(normalized / 30) % 12;
        double degreeInSign = normalized % 30;
        return (SignNames[signIndex], degreeInSign);
    }

    /// <summary>
    /// Formats degree-in-sign as "12° 34'" (degrees and arc minutes).
    /// </summary>
    public static string FormatDegreeMinutes(double degreeInSign)
    {
        int degrees = (int)degreeInSign;
        double fractional = degreeInSign - degrees;
        int minutes = (int)Math.Round(fractional * 60);
        if (minutes >= 60) { minutes = 0; degrees++; }
        return $"{degrees}° {minutes:D2}'";
    }

    /// <summary>
    /// Full formatted line: e.g. "Aries 12° 34'"
    /// </summary>
    public static string FormatSignAndDegree(double longitude)
    {
        var (signName, degreeInSign) = LongitudeToZodiac(longitude);
        return $"{signName} {FormatDegreeMinutes(degreeInSign)}";
    }
}
