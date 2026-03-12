namespace VedicCharts.Core;

/// <summary>
/// One line in the birth chart list: planet/lagna name and position in zodiac.
/// </summary>
public sealed record BirthChartEntry(string BodyName, string SignName, double DegreeInSign)
{
    /// <summary>
    /// Display text e.g. "Sun in Taurus 5° 12'"
    /// </summary>
    public string DisplayText => $"{BodyName} in {SignName} {ZodiacHelper.FormatDegreeMinutes(DegreeInSign)}";
}
