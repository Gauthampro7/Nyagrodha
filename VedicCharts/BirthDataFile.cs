using System;
using System.Text.Json.Serialization;

namespace VedicCharts;

public sealed class BirthDataFile
{
    public int Version { get; init; } = 1;

    public string? Name { get; init; }

    /// <summary>
    /// Local date/time with numeric offset, formatted as: "HH:mm dd/MM/yyyy zzz"
    /// </summary>
    public string StdTime { get; init; } = "";

    public BirthLocation Location { get; init; } = new();

    public ChartSettings? Chart { get; init; }

    public sealed class BirthLocation
    {
        public string Name { get; init; } = "";
        public string? Country { get; init; }
        public double Latitude { get; init; }
        public double Longitude { get; init; }

        /// <summary>
        /// IANA/system timezone id (preferred). Example: "Asia/Kolkata".
        /// </summary>
        public string? TimeZone { get; init; }
    }

    public sealed class ChartSettings
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ChartStyle Style { get; init; } = ChartStyle.South;

        /// <summary>
        /// Chart type id, matching UI entries (e.g., "RasiD1", "NavamshaD9").
        /// </summary>
        public string ChartType { get; init; } = "RasiD1";
    }

    public enum ChartStyle
    {
        South,
        North,
    }
}

