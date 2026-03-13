using System.Globalization;
using Microsoft.VisualBasic.FileIO;

namespace VedicCharts.Core;

/// <summary>
/// Embedded seed data: (Name, Country, Latitude, Longitude, IANA TimeZone).
/// </summary>
public static partial class SeedData
{
    /// <summary>
    /// True if the last call to <see cref="GetCities"/> used the external CSV source.
    /// </summary>
    public static bool UsedCsvLast { get; private set; }

    /// <summary>
    /// Returns a list of cities with coordinates and timezone for seeding the Places table.
    /// </summary>
    public static IReadOnlyList<(string Name, string Country, double Lat, double Lon, string TimeZone)> GetCities()
    {
        // Prefer loading from an external CSV if available so the
        // location database can scale to tens of thousands of rows
        // without bloating the assembly.
        var fromCsv = TryLoadFromCsv();
        if (fromCsv.Count > 0)
        {
            UsedCsvLast = true;
            return fromCsv;
        }

        // Fallback: a small built-in list so the app still works
        // out-of-the-box even if the CSV is missing.
        UsedCsvLast = false;
        return Cities;
    }

    /// <summary>
    /// Attempts to load cities from a CSV file named
    /// "world_cities_nyagrodha.csv" located next to the executable.
    /// Expected columns (header row, order):
    /// Name,Country,Latitude,Longitude,TimeZone
    /// </summary>
    private static IReadOnlyList<(string Name, string Country, double Lat, double Lon, string TimeZone)> TryLoadFromCsv()
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;

            // Primary: next to the running executable (e.g. bin/Debug/net8.0-windows)
            var primaryPath = Path.Combine(baseDir, "world_cities_nyagrodha.csv");

            // Fallback: sibling net8.0 folder (where the initial script wrote the CSV)
            string? candidate = null;
            if (File.Exists(primaryPath))
            {
                candidate = primaryPath;
            }
            else
            {
                var parent = Directory.GetParent(baseDir)?.FullName;
                if (!string.IsNullOrEmpty(parent))
                {
                    var alt = Path.Combine(parent, "net8.0", "world_cities_nyagrodha.csv");
                    if (File.Exists(alt))
                        candidate = alt;
                }
            }

            if (candidate is null || !File.Exists(candidate))
                return Array.Empty<(string, string, double, double, string)>();

            var list = new List<(string, string, double, double, string)>(capacity: 32_000);

            using var parser = new TextFieldParser(candidate)
            {
                TextFieldType = FieldType.Delimited,
                Delimiters = new[] { "," },
                HasFieldsEnclosedInQuotes = true,
                TrimWhiteSpace = true
            };

            // Read header and discover column indices by name
            var header = parser.ReadFields();
            if (header is null)
                return Array.Empty<(string, string, double, double, string)>();

            int idxName = Array.FindIndex(header, h => string.Equals(h, "Name", StringComparison.OrdinalIgnoreCase));
            int idxCountry = Array.FindIndex(header, h => string.Equals(h, "Country", StringComparison.OrdinalIgnoreCase) ||
                                                         string.Equals(h, "Country_name", StringComparison.OrdinalIgnoreCase));
            int idxLat = Array.FindIndex(header, h => string.Equals(h, "Latitude", StringComparison.OrdinalIgnoreCase));
            int idxLon = Array.FindIndex(header, h => string.Equals(h, "Longitude", StringComparison.OrdinalIgnoreCase));
            int idxTz = Array.FindIndex(header, h => string.Equals(h, "TimeZone", StringComparison.OrdinalIgnoreCase) ||
                                                    string.Equals(h, "Timezone", StringComparison.OrdinalIgnoreCase));

            if (idxName < 0 || idxCountry < 0 || idxLat < 0 || idxLon < 0 || idxTz < 0)
                return Array.Empty<(string, string, double, double, string)>();

            while (!parser.EndOfData)
            {
                var fields = parser.ReadFields();
                if (fields is null || fields.Length <= Math.Max(Math.Max(idxLon, idxLat), idxTz))
                    continue;

                var name = fields[idxName].Trim();
                var country = fields[idxCountry].Trim();
                var tz = fields[idxTz].Trim();
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(country) || string.IsNullOrEmpty(tz))
                    continue;

                if (!double.TryParse(fields[idxLat], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat))
                    continue;
                if (!double.TryParse(fields[idxLon], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
                    continue;

                list.Add((name, country, lat, lon, tz));
            }

            return list;
        }
        catch
        {
            return Array.Empty<(string, string, double, double, string)>();
        }
    }

    private static readonly IReadOnlyList<(string Name, string Country, double Lat, double Lon, string TimeZone)> Cities = new List<(string, string, double, double, string)>
    {
        ("Mumbai", "India", 19.0760, 72.8777, "Asia/Kolkata"),
        ("Delhi", "India", 28.7041, 77.1025, "Asia/Kolkata"),
        ("Bangalore", "India", 12.9716, 77.5946, "Asia/Kolkata"),
        ("Chennai", "India", 13.0827, 80.2707, "Asia/Kolkata"),
        ("Kolkata", "India", 22.5726, 88.3639, "Asia/Kolkata"),
        ("Hyderabad", "India", 17.3850, 78.4867, "Asia/Kolkata"),
        ("Pune", "India", 18.5204, 73.8567, "Asia/Kolkata"),
        ("Ahmedabad", "India", 23.0225, 72.5714, "Asia/Kolkata"),
        ("New York", "USA", 40.7128, -74.0060, "America/New_York"),
        ("Los Angeles", "USA", 34.0522, -118.2437, "America/Los_Angeles"),
        ("Chicago", "USA", 41.8781, -87.6298, "America/Chicago"),
        ("Houston", "USA", 29.7604, -95.3698, "America/Chicago"),
        ("London", "UK", 51.5074, -0.1278, "Europe/London"),
        ("Paris", "France", 48.8566, 2.3522, "Europe/Paris"),
        ("Berlin", "Germany", 52.5200, 13.4050, "Europe/Berlin"),
        ("Tokyo", "Japan", 35.6762, 139.6503, "Asia/Tokyo"),
        ("Singapore", "Singapore", 1.3521, 103.8198, "Asia/Singapore"),
        ("Dubai", "UAE", 25.2048, 55.2708, "Asia/Dubai"),
        ("Sydney", "Australia", -33.8688, 151.2093, "Australia/Sydney"),
        ("Melbourne", "Australia", -37.8136, 144.9631, "Australia/Melbourne"),
        ("Toronto", "Canada", 43.6532, -79.3832, "America/Toronto"),
        ("Vancouver", "Canada", 49.2827, -123.1207, "America/Vancouver"),
        ("Hong Kong", "Hong Kong", 22.3193, 114.1694, "Asia/Hong_Kong"),
        ("Bangkok", "Thailand", 13.7563, 100.5018, "Asia/Bangkok"),
        ("Kuala Lumpur", "Malaysia", 3.1390, 101.6869, "Asia/Kuala_Lumpur"),
        ("Jakarta", "Indonesia", -6.2088, 106.8456, "Asia/Jakarta"),
        ("Manila", "Philippines", 14.5995, 120.9842, "Asia/Manila"),
        ("Seoul", "South Korea", 37.5665, 126.9780, "Asia/Seoul"),
        ("Beijing", "China", 39.9042, 116.4074, "Asia/Shanghai"),
        ("Shanghai", "China", 31.2304, 121.4737, "Asia/Shanghai"),
        ("Moscow", "Russia", 55.7558, 37.6173, "Europe/Moscow"),
        ("Istanbul", "Turkey", 41.0082, 28.9784, "Europe/Istanbul"),
        ("Cairo", "Egypt", 30.0444, 31.2357, "Africa/Cairo"),
        ("Johannesburg", "South Africa", -26.2041, 28.0473, "Africa/Johannesburg"),
        ("Mexico City", "Mexico", 19.4326, -99.1332, "America/Mexico_City"),
        ("São Paulo", "Brazil", -23.5505, -46.6333, "America/Sao_Paulo"),
        ("Rio de Janeiro", "Brazil", -22.9068, -43.1729, "America/Sao_Paulo"),
        ("Buenos Aires", "Argentina", -34.6037, -58.3816, "America/Argentina/Buenos_Aires"),
        ("Lagos", "Nigeria", 6.5244, 3.3792, "Africa/Lagos"),
        ("Nairobi", "Kenya", -1.2921, 36.8219, "Africa/Nairobi"),
        ("Riyadh", "Saudi Arabia", 24.7136, 46.6753, "Asia/Riyadh"),
        ("Tel Aviv", "Israel", 32.0853, 34.7818, "Asia/Jerusalem"),
        ("Athens", "Greece", 37.9838, 23.7275, "Europe/Athens"),
        ("Rome", "Italy", 41.9028, 12.4964, "Europe/Rome"),
        ("Madrid", "Spain", 40.4168, -3.7038, "Europe/Madrid"),
        ("Amsterdam", "Netherlands", 52.3676, 4.9041, "Europe/Amsterdam"),
        ("Brussels", "Belgium", 50.8503, 4.3517, "Europe/Brussels"),
        ("Vienna", "Austria", 48.2082, 16.3738, "Europe/Vienna"),
        ("Warsaw", "Poland", 52.2297, 21.0122, "Europe/Warsaw"),
        ("Stockholm", "Sweden", 59.3293, 18.0686, "Europe/Stockholm"),
        ("Oslo", "Norway", 59.9139, 10.7522, "Europe/Oslo"),
        ("Helsinki", "Finland", 60.1695, 24.9354, "Europe/Helsinki"),
        ("Copenhagen", "Denmark", 55.6761, 12.5683, "Europe/Copenhagen"),
        ("Dublin", "Ireland", 53.3498, -6.2603, "Europe/Dublin"),
        ("Lisbon", "Portugal", 38.7223, -9.1393, "Europe/Lisbon"),
        ("Prague", "Czech Republic", 50.0755, 14.4378, "Europe/Prague"),
        ("Budapest", "Hungary", 47.4979, 19.0402, "Europe/Budapest"),
        ("Bucharest", "Romania", 44.4268, 26.1025, "Europe/Bucharest"),
        ("Sofia", "Bulgaria", 42.6977, 23.3219, "Europe/Sofia"),
        ("Kiev", "Ukraine", 50.4501, 30.5234, "Europe/Kiev"),
        ("Minsk", "Belarus", 53.9045, 27.5615, "Europe/Minsk"),
        ("Raipur", "India", 21.2514, 81.6296, "Asia/Kolkata"),
        ("Srinagar", "India", 34.0837, 74.7973, "Asia/Kolkata"),
        ("Kochi", "India", 9.9312, 76.2673, "Asia/Kolkata"),
        ("Thodupuzha", "India", 9.8833, 76.7000, "Asia/Kolkata"),
        ("Thiruvananthapuram", "India", 8.5241, 76.9366, "Asia/Kolkata"),
        ("Coimbatore", "India", 11.0168, 76.9558, "Asia/Kolkata"),
        ("Madurai", "India", 9.9252, 78.1198, "Asia/Kolkata"),
        ("Surat", "India", 21.1702, 72.8311, "Asia/Kolkata"),
        ("Kanpur", "India", 26.4499, 80.3319, "Asia/Kolkata"),
        ("Jaipur", "India", 26.9124, 75.7873, "Asia/Kolkata"),
        ("Lucknow", "India", 26.8467, 80.9462, "Asia/Kolkata"),
        ("Nagpur", "India", 21.1458, 79.0882, "Asia/Kolkata"),
        ("Indore", "India", 22.7196, 75.8577, "Asia/Kolkata"),
        ("Bhopal", "India", 23.2599, 77.4126, "Asia/Kolkata"),
        ("Ludhiana", "India", 30.9010, 75.8573, "Asia/Kolkata"),
        ("Chandigarh", "India", 30.7333, 76.7794, "Asia/Kolkata"),
        ("Guwahati", "India", 26.1445, 91.7362, "Asia/Kolkata"),
        ("Bhubaneswar", "India", 20.2961, 85.8245, "Asia/Kolkata"),
        ("Patna", "India", 25.5941, 85.1376, "Asia/Kolkata"),
        ("Ranchi", "India", 23.3441, 85.3096, "Asia/Kolkata"),
        ("Srinagar", "India", 34.0837, 74.7973, "Asia/Kolkata"),
        ("Dehradun", "India", 30.3165, 78.0322, "Asia/Kolkata"),
        ("Shimla", "India", 31.1048, 77.1734, "Asia/Kolkata"),
        ("Gangtok", "India", 27.3389, 88.6061, "Asia/Kolkata"),
        ("Imphal", "India", 24.8170, 93.9368, "Asia/Kolkata"),
        ("Aizawl", "India", 23.7307, 92.7173, "Asia/Kolkata"),
        ("Kohima", "India", 25.6741, 94.1086, "Asia/Kolkata"),
        ("Itanagar", "India", 27.1026, 93.6953, "Asia/Kolkata"),
        ("Dispur", "India", 26.1433, 91.7898, "Asia/Kolkata"),
        ("Shillong", "India", 25.5788, 91.8933, "Asia/Kolkata"),
        ("Agartala", "India", 23.8315, 91.2868, "Asia/Kolkata"),
        ("Panaji", "India", 15.4909, 73.8278, "Asia/Kolkata"),
        ("Port Blair", "India", 11.6234, 92.7265, "Asia/Kolkata"),
        ("Karachi", "Pakistan", 24.8607, 67.0011, "Asia/Karachi"),
        ("Lahore", "Pakistan", 31.5204, 74.3587, "Asia/Karachi"),
        ("Islamabad", "Pakistan", 33.6844, 73.0479, "Asia/Karachi"),
        ("Dhaka", "Bangladesh", 23.8103, 90.4125, "Asia/Dhaka"),
        ("Colombo", "Sri Lanka", 6.9271, 79.8612, "Asia/Colombo"),
        ("Kathmandu", "Nepal", 27.7172, 85.3240, "Asia/Kathmandu"),
        ("Thimphu", "Bhutan", 27.4728, 89.6390, "Asia/Thimphu"),
        ("Male", "Maldives", 4.1755, 73.5093, "Indian/Maldives"),
        ("Kabul", "Afghanistan", 34.5553, 69.2075, "Asia/Kabul"),
        ("Tehran", "Iran", 35.6892, 51.3890, "Asia/Tehran"),
        ("Baghdad", "Iraq", 33.3152, 44.3661, "Asia/Baghdad"),
        ("Riyadh", "Saudi Arabia", 24.7136, 46.6753, "Asia/Riyadh"),
        ("Jeddah", "Saudi Arabia", 21.5433, 39.1728, "Asia/Riyadh"),
        ("Doha", "Qatar", 25.2854, 51.5310, "Asia/Qatar"),
        ("Kuwait City", "Kuwait", 29.3759, 47.9774, "Asia/Kuwait"),
        ("Muscat", "Oman", 23.5880, 58.3829, "Asia/Muscat"),
        ("Sana'a", "Yemen", 15.3694, 44.1910, "Asia/Aden"),
        ("Amman", "Jordan", 31.9454, 35.9284, "Asia/Amman"),
        ("Beirut", "Lebanon", 33.8938, 35.5018, "Asia/Beirut"),
        ("Damascus", "Syria", 33.5138, 36.2765, "Asia/Damascus"),
        ("Abu Dhabi", "UAE", 24.4539, 54.3773, "Asia/Dubai"),
        ("Sharjah", "UAE", 25.3573, 55.4033, "Asia/Dubai"),
        ("Abuja", "Nigeria", 9.0765, 7.3986, "Africa/Lagos"),
        ("Accra", "Ghana", 5.6037, -0.1870, "Africa/Accra"),
        ("Addis Ababa", "Ethiopia", 9.0320, 38.7469, "Africa/Addis_Ababa"),
        ("Dar es Salaam", "Tanzania", -6.7924, 39.2083, "Africa/Dar_es_Salaam"),
        ("Kampala", "Uganda", 0.3476, 32.5825, "Africa/Kampala"),
        ("Lusaka", "Zambia", -15.3875, 28.3228, "Africa/Lusaka"),
        ("Harare", "Zimbabwe", -17.8292, 31.0522, "Africa/Harare"),
        ("Maputo", "Mozambique", -25.9692, 32.5732, "Africa/Maputo"),
        ("Algiers", "Algeria", 36.7538, 3.0588, "Africa/Algiers"),
        ("Casablanca", "Morocco", 33.5731, -7.5898, "Africa/Casablanca"),
        ("Tunis", "Tunisia", 36.8065, 10.1815, "Africa/Tunis"),
        ("Lima", "Peru", -12.0464, -77.0428, "America/Lima"),
        ("Bogotá", "Colombia", 4.7110, -74.0721, "America/Bogota"),
        ("Caracas", "Venezuela", 10.4806, -66.9036, "America/Caracas"),
        ("Santiago", "Chile", -33.4489, -70.6693, "America/Santiago"),
        ("Lima", "Peru", -12.0464, -77.0428, "America/Lima"),
        ("Quito", "Ecuador", -0.1807, -78.4678, "America/Guayaquil"),
        ("Panama City", "Panama", 9.0820, -79.5199, "America/Panama"),
        ("San José", "Costa Rica", 9.9281, -84.0907, "America/Costa_Rica"),
        ("Havana", "Cuba", 23.1136, -82.3666, "America/Havana"),
        ("Kingston", "Jamaica", 18.0179, -76.8099, "America/Jamaica"),
        ("San Juan", "Puerto Rico", 18.4655, -66.1057, "America/Puerto_Rico"),
        ("Anchorage", "USA", 61.2181, -149.9003, "America/Anchorage"),
        ("Honolulu", "USA", 21.3099, -157.8581, "Pacific/Honolulu"),
        ("Phoenix", "USA", 33.4484, -112.0740, "America/Phoenix"),
        ("Denver", "USA", 39.7392, -104.9903, "America/Denver"),
        ("San Francisco", "USA", 37.7749, -122.4194, "America/Los_Angeles"),
        ("Seattle", "USA", 47.6062, -122.3321, "America/Los_Angeles"),
        ("Boston", "USA", 42.3601, -71.0589, "America/New_York"),
        ("Philadelphia", "USA", 39.9526, -75.1652, "America/New_York"),
        ("Washington", "USA", 38.9072, -77.0369, "America/New_York"),
        ("Atlanta", "USA", 33.7490, -84.3880, "America/New_York"),
        ("Miami", "USA", 25.7617, -80.1918, "America/New_York"),
        ("Dallas", "USA", 32.7767, -96.7970, "America/Chicago"),
        ("Montreal", "Canada", 45.5017, -73.5673, "America/Montreal"),
        ("Calgary", "Canada", 51.0447, -114.0719, "America/Edmonton"),
        ("Ottawa", "Canada", 45.4215, -75.6972, "America/Toronto"),
        ("Auckland", "New Zealand", -36.8509, 174.7645, "Pacific/Auckland"),
        ("Wellington", "New Zealand", -41.2866, 174.7756, "Pacific/Auckland"),
        ("Christchurch", "New Zealand", -43.5321, 172.6362, "Pacific/Auckland"),
        ("Perth", "Australia", -31.9505, 115.8605, "Australia/Perth"),
        ("Brisbane", "Australia", -27.4698, 153.0251, "Australia/Brisbane"),
        ("Adelaide", "Australia", -34.9285, 138.6007, "Australia/Adelaide"),
        ("Darwin", "Australia", -12.4634, 130.8456, "Australia/Darwin"),
        ("Hobart", "Australia", -42.8821, 147.3272, "Australia/Hobart"),
    };
}
