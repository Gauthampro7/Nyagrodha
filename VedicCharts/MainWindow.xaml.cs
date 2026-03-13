using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using VedicCharts.Controls;
using VedicCharts.Core;

namespace VedicCharts;

public partial class MainWindow : Window
{
    private readonly PlacesRepository _places;
    private Place? _selectedPlace;

    public MainWindow()
    {
        InitializeComponent();
        _places = new PlacesRepository();
        _places.EnsureDatabase();
        _places.SeedIfEmpty();
        _places.EnsureDefaultPlace();
        BirthDatePicker.SelectedDate = new System.DateTime(2003, 5, 15);
        BirthTimeBox.Text = "17:55";

        ChartStyleCombo.ItemsSource = new[]
        {
            new ChartStyleItem { Id = ChartStyle.South, DisplayText = "South Indian" },
            new ChartStyleItem { Id = ChartStyle.North, DisplayText = "North Indian" },
        };
        ChartStyleCombo.SelectedIndex = 0;

        ChartTypeCombo.ItemsSource = ChartTypeItem.All;
        ChartTypeCombo.SelectedIndex = 0;

        AyanamsaCombo.ItemsSource = new[]
        {
            new AyanamsaItem { Id = "Lahiri", DisplayText = "Lahiri (Chitra Paksha)" },
            new AyanamsaItem { Id = "Raman", DisplayText = "Raman" },
            new AyanamsaItem { Id = "Krishnamurti", DisplayText = "Krishnamurti (KP)" },
            new AyanamsaItem { Id = "FaganBradley", DisplayText = "Fagan-Bradley" },
            new AyanamsaItem { Id = "Yukteshwar", DisplayText = "Yukteshwar" },
        };
        AyanamsaCombo.SelectedIndex = 0;
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Set default place: Thodupuzha, India (76°42'E, 9°53'N)
        PlaceSearchBox.Text = "Thodupuzha";
        var results = _places.Search("Thodupuzha", 10);
        var list = results.Select(p => new PlaceDisplay(p)).ToList();
        PlaceResultsList.ItemsSource = list;
        if (list.Count > 0)
        {
            PlaceResultsList.SelectedIndex = 0;
            _selectedPlace = list[0].Place;
            CoordsText.Text = $"{_selectedPlace.Latitude:F4}, {_selectedPlace.Longitude:F4} ({_selectedPlace.TimeZone})";
        }

        var placeCount = _places.Count();
        if (SeedData.UsedCsvLast && placeCount > 1000)
        {
            LocationDbCue.Text = $"Extended database active ({placeCount:N0} locations)";
        }
        else if (placeCount > 0)
        {
            LocationDbCue.Text = $"Built-in location list ({placeCount:N0})";
        }
        else
        {
            LocationDbCue.Text = "";
        }
    }

    private void PlaceSearchBox_KeyUp(object sender, KeyEventArgs e)
    {
        var query = PlaceSearchBox.Text?.Trim() ?? "";
        if (query.Length < 2)
        {
            PlaceResultsList.ItemsSource = null;
            return;
        }
        var results = _places.Search(query, 30);
        PlaceResultsList.ItemsSource = results.Select(p => new PlaceDisplay(p)).ToList();
    }

    private void PlaceResultsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (PlaceResultsList.SelectedItem is PlaceDisplay pd)
        {
            _selectedPlace = pd.Place;
            CoordsText.Text = $"{pd.Place.Latitude:F4}, {pd.Place.Longitude:F4} ({pd.Place.TimeZone})";
        }
        else
        {
            _selectedPlace = null;
            CoordsText.Text = "—";
        }
    }

    private void CalculateButton_Click(object sender, RoutedEventArgs e)
    {
        if (!BirthDatePicker.SelectedDate.HasValue)
        {
            MessageBox.Show("Please select a birth date.", "Nyagrodha", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var date = DateOnly.FromDateTime(BirthDatePicker.SelectedDate.Value);

        if (!TryParseTime(BirthTimeBox.Text?.Trim() ?? "", out int hours, out int minutes))
        {
            MessageBox.Show("Please enter birth time as HH:mm (24-hour).", "Nyagrodha", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_selectedPlace == null)
        {
            MessageBox.Show("Please search and select a birth place.", "Nyagrodha", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var birthDateTime = new System.DateTime(date.Year, date.Month, date.Day, hours, minutes, 0);
            double offsetHours = _loadedOffsetOverrideHours ?? TimeZoneHelper.GetOffsetHours(
                _selectedPlace.TimeZone,
                birthDateTime,
                _selectedPlace.Longitude);
            _loadedOffsetOverrideHours = null;

            string? ayanamsaId = (AyanamsaCombo.SelectedItem as AyanamsaItem)?.Id;
            string chartTypeId = (ChartTypeCombo.SelectedItem as ChartTypeItem)?.Id ?? "RasiD1";
            var style = (ChartStyleCombo.SelectedItem as ChartStyleItem)?.Id ?? ChartStyle.South;

            var entries = BirthChartCalculator.Calculate(
                date,
                (hours, minutes, 0),
                _selectedPlace.Latitude,
                _selectedPlace.Longitude,
                offsetHours,
                ayanamsaId);

            ChartListBox.ItemsSource = entries;

            var chartData = BirthChartCalculator.CalculateChartData(
                chartTypeId,
                date,
                (hours, minutes, 0),
                _selectedPlace.Latitude,
                _selectedPlace.Longitude,
                offsetHours,
                ayanamsaId);

            IndianChart.ChartStyle = style == ChartStyle.North ? Controls.ChartStyle.North : Controls.ChartStyle.South;
            IndianChart.Houses = chartData.Houses
                .Select(h => new HouseCell(h.HouseNumber, h.SignName, h.Bodies))
                .ToList();
        }
        catch (System.Exception ex)
        {
            MessageBox.Show("Calculation failed: " + ex.Message, "Nyagrodha", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static bool TryParseTime(string s, out int hours, out int minutes)
    {
        hours = 0;
        minutes = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        var parts = s.Split(':');
        if (parts.Length < 2) return false;
        if (!int.TryParse(parts[0].Trim(), out hours) || hours < 0 || hours > 23) return false;
        if (!int.TryParse(parts[1].Trim(), out minutes) || minutes < 0 || minutes > 59) return false;
        return true;
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        ChartListBox.ItemsSource = null;
        IndianChart.Houses = Array.Empty<HouseCell>();
        PlaceResultsList.SelectedItem = null;
        _selectedPlace = null;
        CoordsText.Text = "—";
    }

    private void CopyChartImageButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            IndianChart.UpdateLayout();
            if (IndianChart.ActualWidth <= 0 || IndianChart.ActualHeight <= 0)
                return;

            var width = (int)Math.Ceiling(IndianChart.ActualWidth);
            var height = (int)Math.Ceiling(IndianChart.ActualHeight);

            var rtb = new RenderTargetBitmap(
                width,
                height,
                96, 96,
                PixelFormats.Pbgra32);

            rtb.Render(IndianChart);
            Clipboard.SetImage(rtb);
            MessageBox.Show("Chart image copied to clipboard.", "Nyagrodha", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Copy chart image failed: " + ex.Message, "Nyagrodha", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CopyChartTextButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ChartListBox.ItemsSource is not IEnumerable<BirthChartEntry> entries)
            {
                MessageBox.Show("Create a chart first.", "Nyagrodha", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sb = new StringBuilder();
            foreach (var entry in entries)
            {
                sb.AppendLine(entry.ToString());
            }

            Clipboard.SetText(sb.ToString());
            MessageBox.Show("Chart details copied as text.", "Nyagrodha", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Copy chart text failed: " + ex.Message, "Nyagrodha", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenMenuItem_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dlg = new OpenFileDialog
            {
                Title = "Open birth data",
                Filter = "Nyagrodha birth data (*.ny)|*.ny|JSON (*.json)|*.json|All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false,
            };
            if (dlg.ShowDialog(this) != true) return;

            var json = System.IO.File.ReadAllText(dlg.FileName);
            var data = JsonSerializer.Deserialize<BirthDataFile>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
            if (data == null) throw new System.Exception("File is empty or invalid JSON.");
            if (string.IsNullOrWhiteSpace(data.StdTime)) throw new System.Exception("Missing StdTime.");

            if (!TryParseStdTime(data.StdTime, out var date, out var hours, out var minutes, out var offsetHours))
                throw new System.Exception("StdTime must be formatted like \"HH:mm dd/MM/yyyy zzz\".");

            if (data.Location is null) throw new System.Exception("Missing Location.");
            if (data.Location.Latitude is < -90 or > 90) throw new System.Exception("Invalid latitude.");
            if (data.Location.Longitude is < -180 or > 180) throw new System.Exception("Invalid longitude.");

            BirthDatePicker.SelectedDate = new System.DateTime(date.Year, date.Month, date.Day);
            BirthTimeBox.Text = $"{hours:D2}:{minutes:D2}";

            // Populate place list with the loaded location (no DB dependency)
            var place = new Place(
                Id: 0,
                Name: data.Location.Name ?? "",
                Country: data.Location.Country ?? "",
                Latitude: data.Location.Latitude,
                Longitude: data.Location.Longitude,
                TimeZone: data.Location.TimeZone ?? "");

            _selectedPlace = place;
            PlaceSearchBox.Text = place.Name;
            PlaceResultsList.ItemsSource = new[] { new PlaceDisplay(place) };
            PlaceResultsList.SelectedIndex = 0;
            CoordsText.Text = $"{place.Latitude:F4}, {place.Longitude:F4} ({place.TimeZone})";

            // Restore chart settings (best-effort)
            if (data.Chart != null)
            {
                ChartStyleCombo.SelectedItem = ((IEnumerable<ChartStyleItem>)ChartStyleCombo.ItemsSource)
                    .FirstOrDefault(x => x.Id.ToString() == data.Chart.Style.ToString()) ?? ChartStyleCombo.SelectedItem;

                ChartTypeCombo.SelectedItem = ((IEnumerable<ChartTypeItem>)ChartTypeCombo.ItemsSource)
                    .FirstOrDefault(x => x.Id == data.Chart.ChartType) ?? ChartTypeCombo.SelectedItem;
            }

            // Keep calculation stable: if offset in StdTime differs from tz, we use the offset from StdTime.
            // (The calculator already accepts explicit offset hours.)
            _loadedOffsetOverrideHours = offsetHours;

            ChartListBox.ItemsSource = null;
        }
        catch (System.Exception ex)
        {
            MessageBox.Show("Open failed: " + ex.Message, "Nyagrodha", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveMenuItem_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var current = GetCurrentBirthDataFileOrThrow();
            var dlg = new SaveFileDialog
            {
                Title = "Save birth data",
                Filter = "Nyagrodha birth data (*.ny)|*.ny|All files (*.*)|*.*",
                AddExtension = true,
                DefaultExt = ".ny",
                FileName = "birth-data.ny",
                OverwritePrompt = true,
            };
            if (dlg.ShowDialog(this) != true) return;

            var json = JsonSerializer.Serialize(current, new JsonSerializerOptions
            {
                WriteIndented = true,
            });
            System.IO.File.WriteAllText(dlg.FileName, json);
        }
        catch (System.Exception ex)
        {
            MessageBox.Show("Save failed: " + ex.Message, "Nyagrodha", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e) => Close();

    private sealed class PlaceDisplay
    {
        public Place Place { get; }
        public string DisplayText => $"{Place.Name}, {Place.Country}";
        public PlaceDisplay(Place place) => Place = place;
    }

    /// <summary>For Ayanamsa dropdown: Id is the value passed to calculator, DisplayText is shown in UI.</summary>
    public sealed class AyanamsaItem
    {
        public string Id { get; init; } = "";
        public string DisplayText { get; init; } = "";
        public override string ToString() => DisplayText;
    }

    private enum ChartStyle
    {
        South,
        North,
    }

    private sealed class ChartStyleItem
    {
        public ChartStyle Id { get; init; }
        public string DisplayText { get; init; } = "";
        public override string ToString() => DisplayText;
    }

    private sealed class ChartTypeItem
    {
        public string Id { get; init; } = "";
        public string DisplayText { get; init; } = "";
        public override string ToString() => DisplayText;

        public static IReadOnlyList<ChartTypeItem> All { get; } = new[]
        {
            new ChartTypeItem { Id = "RasiD1", DisplayText = "D1 — Rasi" },
            new ChartTypeItem { Id = "HoraD2", DisplayText = "D2 — Hora" },
            new ChartTypeItem { Id = "DrekkanaD3", DisplayText = "D3 — Drekkana" },
            new ChartTypeItem { Id = "ChaturthamshaD4", DisplayText = "D4 — Chaturthamsha" },
            new ChartTypeItem { Id = "SaptamshaD7", DisplayText = "D7 — Saptamsha" },
            new ChartTypeItem { Id = "NavamshaD9", DisplayText = "D9 — Navamsha" },
            new ChartTypeItem { Id = "DashamamshaD10", DisplayText = "D10 — Dashamamsha" },
            new ChartTypeItem { Id = "DwadashamshaD12", DisplayText = "D12 — Dwadashamsha" },
            new ChartTypeItem { Id = "ShodashamshaD16", DisplayText = "D16 — Shodashamsha" },
            new ChartTypeItem { Id = "VimshamshaD20", DisplayText = "D20 — Vimshamsha" },
            new ChartTypeItem { Id = "ChaturvimshamshaD24", DisplayText = "D24 — Chaturvimshamsha" },
            new ChartTypeItem { Id = "BhamshaD27", DisplayText = "D27 — Bhamsha" },
            new ChartTypeItem { Id = "TrimshamshaD30", DisplayText = "D30 — Trimshamsha" },
            new ChartTypeItem { Id = "KhavedamshaD40", DisplayText = "D40 — Khavedamsha" },
            new ChartTypeItem { Id = "AkshavedamshaD45", DisplayText = "D45 — Akshavedamsha" },
            new ChartTypeItem { Id = "ShashtyamshaD60", DisplayText = "D60 — Shashtyamsha" },
        };
    }

    private double? _loadedOffsetOverrideHours;

    private BirthDataFile GetCurrentBirthDataFileOrThrow()
    {
        if (!BirthDatePicker.SelectedDate.HasValue)
            throw new System.Exception("Please select a birth date.");

        if (!TryParseTime(BirthTimeBox.Text?.Trim() ?? "", out int hours, out int minutes))
            throw new System.Exception("Please enter birth time as HH:mm (24-hour).");

        if (_selectedPlace == null)
            throw new System.Exception("Please search and select a birth place.");

        var date = DateOnly.FromDateTime(BirthDatePicker.SelectedDate.Value);
        var birthDateTime = new System.DateTime(date.Year, date.Month, date.Day, hours, minutes, 0);

        double offsetHours = TimeZoneHelper.GetOffsetHours(
            _selectedPlace.TimeZone,
            birthDateTime,
            _selectedPlace.Longitude);

        string offsetStr = BirthChartCalculatorFormatOffset(offsetHours);
        string stdTime = $"{hours:D2}:{minutes:D2} {date.Day:D2}/{date.Month:D2}/{date.Year} {offsetStr}";

        var style = (ChartStyleCombo.SelectedItem as ChartStyleItem)?.Id ?? ChartStyle.South;
        var chartType = (ChartTypeCombo.SelectedItem as ChartTypeItem)?.Id ?? "RasiD1";

        return new BirthDataFile
        {
            StdTime = stdTime,
            Location = new BirthDataFile.BirthLocation
            {
                Name = _selectedPlace.Name,
                Country = _selectedPlace.Country,
                Latitude = _selectedPlace.Latitude,
                Longitude = _selectedPlace.Longitude,
                TimeZone = _selectedPlace.TimeZone,
            },
            Chart = new BirthDataFile.ChartSettings
            {
                Style = style == ChartStyle.North ? BirthDataFile.ChartStyle.North : BirthDataFile.ChartStyle.South,
                ChartType = chartType,
            },
        };
    }

    private static bool TryParseStdTime(string stdTime, out DateOnly date, out int hours, out int minutes, out double offsetHours)
    {
        date = default;
        hours = 0;
        minutes = 0;
        offsetHours = 0;

        // Expected: "HH:mm dd/MM/yyyy zzz"
        if (!System.DateTimeOffset.TryParseExact(stdTime, "HH:mm dd/MM/yyyy zzz", null,
                System.Globalization.DateTimeStyles.None, out var dto))
            return false;

        date = DateOnly.FromDateTime(dto.DateTime);
        hours = dto.Hour;
        minutes = dto.Minute;
        offsetHours = dto.Offset.TotalHours;
        return true;
    }

    private static string BirthChartCalculatorFormatOffset(double timeZoneOffsetHours)
    {
        // Keep exact formatting logic consistent with VedicCharts.Core's internal formatting.
        int hours = (int)timeZoneOffsetHours;
        double frac = timeZoneOffsetHours - hours;
        if (frac < 0) frac += 1;
        int minutes = (int)System.Math.Round(frac * 60);
        if (minutes >= 60) { minutes = 0; hours += 1; }
        if (minutes < 0) minutes = 0;
        return timeZoneOffsetHours >= 0
            ? $"+{hours:D2}:{minutes:D2}"
            : $"-{System.Math.Abs(hours):D2}:{minutes:D2}";
    }
}
