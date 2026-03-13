namespace VedicCharts.Core;

/// <summary>
/// A single body (planet or node) in a house, with its short name and
/// longitude in degrees within the current sign (0–30).
/// </summary>
public sealed record VedicChartBody(
    string ShortName,
    double DegreeInSign);

public sealed record VedicChartHouse(
    int HouseNumber,
    string SignName,
    IReadOnlyList<VedicChartBody> Bodies);

public sealed record VedicChartData(
    string ChartTypeId,
    IReadOnlyList<VedicChartHouse> Houses);

