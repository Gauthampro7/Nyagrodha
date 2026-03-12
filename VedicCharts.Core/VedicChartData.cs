namespace VedicCharts.Core;

public sealed record VedicChartHouse(
    int HouseNumber,
    string SignName,
    IReadOnlyList<string> Bodies);

public sealed record VedicChartData(
    string ChartTypeId,
    IReadOnlyList<VedicChartHouse> Houses);

