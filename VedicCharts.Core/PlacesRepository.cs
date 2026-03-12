using Microsoft.Data.Sqlite;

namespace VedicCharts.Core;

/// <summary>
/// SQLite-backed repository for birth places with search and seed support.
/// </summary>
public sealed class PlacesRepository : IDisposable
{
    private readonly string _dbPath;

    public PlacesRepository(string? dbPath = null)
    {
        _dbPath = dbPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VedicCharts", "vedic_charts.db");
    }

    private string ConnectionString => new SqliteConnectionStringBuilder { DataSource = _dbPath }.ConnectionString;

    public void EnsureDatabase()
    {
        var dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Places (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Country TEXT NOT NULL,
                Latitude REAL NOT NULL,
                Longitude REAL NOT NULL,
                TimeZone TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Places_Name ON Places(Name);
            CREATE INDEX IF NOT EXISTS IX_Places_Country ON Places(Country);
            """;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Search places by name or country (case-insensitive contains).
    /// </summary>
    public IReadOnlyList<Place> Search(string query, int maxResults = 50)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<Place>();

        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, Name, Country, Latitude, Longitude, TimeZone
            FROM Places
            WHERE Name LIKE $q ESCAPE '\' OR Country LIKE $q ESCAPE '\'
            ORDER BY Name LIMIT $max
            """;
        cmd.Parameters.AddWithValue("$q", "%" + EscapeLike(query.Trim()) + "%");
        cmd.Parameters.AddWithValue("$max", maxResults);

        var list = new List<Place>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(ReadPlace(r));
        return list;
    }

    /// <summary>Escape % and _ for SQLite LIKE so they match literally.</summary>
    private static string EscapeLike(string value)
    {
        return value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
    }

    public Place? GetById(long id)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Country, Latitude, Longitude, TimeZone FROM Places WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        using var r = cmd.ExecuteReader();
        return r.Read() ? ReadPlace(r) : null;
    }

    public int Count()
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Places";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>
    /// Seed the database from the embedded cities CSV if the table is empty.
    /// </summary>
    public void SeedIfEmpty()
    {
        if (Count() > 0) return;
        SeedFromEmbedded();
    }

    /// <summary>
    /// Ensures the default place (Thodupuzha, India) exists so default birth data can select it.
    /// </summary>
    public void EnsureDefaultPlace()
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM Places WHERE Name = 'Thodupuzha' AND Country = 'India' LIMIT 1";
        if (cmd.ExecuteScalar() != null) return; // already exists
        cmd.CommandText = "INSERT INTO Places (Name, Country, Latitude, Longitude, TimeZone) VALUES ('Thodupuzha', 'India', 9.8833, 76.7000, 'Asia/Kolkata')";
        cmd.ExecuteNonQuery();
    }

    private void SeedFromEmbedded()
    {
        var cities = SeedData.GetCities();
        if (cities.Count == 0) return;

        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var trans = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO Places (Name, Country, Latitude, Longitude, TimeZone) VALUES ($n, $c, $lat, $lon, $tz)";
        foreach (var (name, country, lat, lon, tz) in cities)
        {
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$n", name);
            cmd.Parameters.AddWithValue("$c", country);
            cmd.Parameters.AddWithValue("$lat", lat);
            cmd.Parameters.AddWithValue("$lon", lon);
            cmd.Parameters.AddWithValue("$tz", tz);
            cmd.ExecuteNonQuery();
        }
        trans.Commit();
    }

    private static Place ReadPlace(SqliteDataReader r) =>
        new(
            r.GetInt64(0),
            r.GetString(1),
            r.GetString(2),
            r.GetDouble(3),
            r.GetDouble(4),
            r.GetString(5));

    public void Dispose() { }
}
