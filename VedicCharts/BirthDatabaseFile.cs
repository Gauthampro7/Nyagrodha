using System.Collections.Generic;

namespace VedicCharts;

/// <summary>
/// A database of multiple birth records. Saved as JSON with extension .nydb.
/// </summary>
public sealed class BirthDatabaseFile
{
    public int Version { get; init; } = 1;

    /// <summary>Optional name or description for the database.</summary>
    public string? Name { get; init; }

    /// <summary>Birth data entries. Mutated in memory when user adds/removes/edits.</summary>
    public List<BirthDataFile> Entries { get; init; } = new();
}
