namespace VedicCharts.Tests;

/// <summary>
/// Verifies that changing Ayanamsa actually changes the chart output (fix for "same output for all Ayanamsa").
/// </summary>
public class AyanamsaVerificationTests
{
    private static readonly DateOnly BirthDate = new(2003, 5, 15);
    private static readonly (int H, int M, int S) BirthTime = (17, 55, 0);
    private const double Latitude = 9.88;
    private const double Longitude = 76.7;
    private const double TimeZoneOffsetHours = 5.5;

    [Fact]
    public void Lahiri_and_Raman_produce_different_chart_entries()
    {
        var lahiri = VedicCharts.Core.BirthChartCalculator.Calculate(
            BirthDate, (BirthTime.H, BirthTime.M, BirthTime.S), Latitude, Longitude, TimeZoneOffsetHours, "Lahiri");
        var raman = VedicCharts.Core.BirthChartCalculator.Calculate(
            BirthDate, (BirthTime.H, BirthTime.M, BirthTime.S), Latitude, Longitude, TimeZoneOffsetHours, "Raman");

        Assert.NotNull(lahiri);
        Assert.NotNull(raman);
        Assert.Equal(lahiri.Count, raman.Count);

        bool atLeastOneDifferent = false;
        for (int i = 0; i < lahiri.Count; i++)
        {
            var a = lahiri[i];
            var b = raman[i];
            if (a.SignName != b.SignName || Math.Abs(a.DegreeInSign - b.DegreeInSign) > 0.001)
            {
                atLeastOneDifferent = true;
                break;
            }
        }
        Assert.True(atLeastOneDifferent,
            "Lahiri and Raman must produce at least one different position (sign or degree). " +
            "If they are identical, the Ayanamsa selection is not being applied.");
    }

    [Fact]
    public void Krishnamurti_differs_from_Lahiri()
    {
        var lahiri = VedicCharts.Core.BirthChartCalculator.Calculate(
            BirthDate, (BirthTime.H, BirthTime.M, BirthTime.S), Latitude, Longitude, TimeZoneOffsetHours, "Lahiri");
        var kp = VedicCharts.Core.BirthChartCalculator.Calculate(
            BirthDate, (BirthTime.H, BirthTime.M, BirthTime.S), Latitude, Longitude, TimeZoneOffsetHours, "Krishnamurti");

        Assert.NotNull(lahiri);
        Assert.NotNull(kp);
        bool atLeastOneDifferent = false;
        for (int i = 0; i < Math.Min(lahiri.Count, kp.Count); i++)
        {
            if (lahiri[i].SignName != kp[i].SignName || Math.Abs(lahiri[i].DegreeInSign - kp[i].DegreeInSign) > 0.001)
            {
                atLeastOneDifferent = true;
                break;
            }
        }
        Assert.True(atLeastOneDifferent, "Krishnamurti and Lahiri must differ for at least one body.");
    }
}
