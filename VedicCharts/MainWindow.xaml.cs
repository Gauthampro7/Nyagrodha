using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using VedicCharts.Controls;
using VedicCharts.Core;

namespace VedicCharts;

public enum AppMode
{
    SingleBirth,
    BirthDatabase,
}

public partial class MainWindow : Window
{
    private readonly PlacesRepository _places;
    private Place? _selectedPlace;

    /// <summary>Stored after a successful calculation so slot combo changes can refresh that slot only.</summary>
    private (DateOnly date, int hours, int minutes, double lat, double lon, double offsetHours, string? ayanamsaId)? _lastCalculationParams;

    // Birth database state
    private AppMode _appMode = AppMode.SingleBirth;
    private BirthDatabaseFile? _birthDatabase;
    private string? _currentDatabasePath;
    private int _selectedDatabaseIndex = -1;
    private readonly ObservableCollection<BirthEntryDisplay> _databaseEntryDisplays = new();

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

        InitSlotCombos();

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

        RegisterKeyboardShortcuts();
    }

    private void RegisterKeyboardShortcuts()
    {
        var createChartCmd = new RoutedCommand("CreateChart", typeof(MainWindow));
        CommandBindings.Add(new CommandBinding(createChartCmd, (s, e) => CalculateButton_Click(s, e)));
        InputBindings.Add(new KeyBinding(createChartCmd, Key.Enter, ModifierKeys.Control));

        var clearCmd = new RoutedCommand("Clear", typeof(MainWindow));
        CommandBindings.Add(new CommandBinding(clearCmd, (s, e) => ClearButton_Click(s, e)));
        InputBindings.Add(new KeyBinding(clearCmd, Key.C, ModifierKeys.Control | ModifierKeys.Shift));
        InputBindings.Add(new KeyBinding(clearCmd, Key.Escape, ModifierKeys.None));

        var copyImageCmd = new RoutedCommand("CopyChartImage", typeof(MainWindow));
        CommandBindings.Add(new CommandBinding(copyImageCmd, (s, e) => CopyChartImageButton_Click(s, e)));
        InputBindings.Add(new KeyBinding(copyImageCmd, Key.I, ModifierKeys.Control | ModifierKeys.Shift));

        var copyTextCmd = new RoutedCommand("CopyChartText", typeof(MainWindow));
        CommandBindings.Add(new CommandBinding(copyTextCmd, (s, e) => CopyChartTextButton_Click(s, e)));
        InputBindings.Add(new KeyBinding(copyTextCmd, Key.T, ModifierKeys.Control | ModifierKeys.Shift));

        var styleSouthCmd = new RoutedCommand("StyleSouth", typeof(MainWindow));
        CommandBindings.Add(new CommandBinding(styleSouthCmd, (s, e) => { ChartStyleCombo.SelectedIndex = 0; }));
        InputBindings.Add(new KeyBinding(styleSouthCmd, Key.D1, ModifierKeys.Control));

        var styleNorthCmd = new RoutedCommand("StyleNorth", typeof(MainWindow));
        CommandBindings.Add(new CommandBinding(styleNorthCmd, (s, e) => { ChartStyleCombo.SelectedIndex = 1; }));
        InputBindings.Add(new KeyBinding(styleNorthCmd, Key.D2, ModifierKeys.Control));

        for (int i = 0; i < Math.Min(9, ChartTypeItem.All.Count); i++)
        {
            int index = i;
            var cmd = new RoutedCommand($"ChartType{index}", typeof(MainWindow));
            CommandBindings.Add(new CommandBinding(cmd, (s, e) =>
            {
                ChartTypeCombo.SelectedIndex = index;
                Slot0ChartTypeCombo.SelectedIndex = index;
                if (_lastCalculationParams is { } p)
                {
                    var item = ChartTypeItem.All[index];
                    var chartData = BirthChartCalculator.CalculateChartData(item.Id, p.date, (p.hours, p.minutes, 0), p.lat, p.lon, p.offsetHours, p.ayanamsaId);
                    var style = (ChartStyleCombo.SelectedItem as ChartStyleItem)?.Id ?? ChartStyle.South;
                    IndianChartSlot0.ChartStyle = style == ChartStyle.North ? Controls.ChartStyle.North : Controls.ChartStyle.South;
                    IndianChartSlot0.Houses = chartData.Houses.Select(h => new HouseCell(h.HouseNumber, h.SignName, h.Bodies)).ToList();
                }
            }));
            InputBindings.Add(new KeyBinding(cmd, Key.D1 + index, ModifierKeys.Control | ModifierKeys.Alt));
        }

        var ayanamsaFocusCmd = new RoutedCommand("AyanamsaFocus", typeof(MainWindow));
        CommandBindings.Add(new CommandBinding(ayanamsaFocusCmd, (s, e) => { AyanamsaCombo.Focus(); AyanamsaCombo.IsDropDownOpen = true; }));
        InputBindings.Add(new KeyBinding(ayanamsaFocusCmd, Key.A, ModifierKeys.Alt));
    }

    private void InitSlotCombos()
    {
        var all = ChartTypeItem.All;
        foreach (var combo in new[] { Slot0ChartTypeCombo, Slot1ChartTypeCombo, Slot2ChartTypeCombo, Slot3ChartTypeCombo })
        {
            combo.ItemsSource = all;
        }
        Slot0ChartTypeCombo.SelectedIndex = 0;   // D1 Rasi
        Slot1ChartTypeCombo.SelectedIndex = IndexOfChartType("NavamshaD9");   // D9
        Slot2ChartTypeCombo.SelectedIndex = IndexOfChartType("SaptamshaD7");   // D7
        Slot3ChartTypeCombo.SelectedIndex = IndexOfChartType("ChaturthamshaD4"); // D4
    }

    private static int IndexOfChartType(string id)
    {
        var all = ChartTypeItem.All;
        for (int i = 0; i < all.Count; i++)
            if (all[i].Id == id) return i;
        return 0;
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
            var style = (ChartStyleCombo.SelectedItem as ChartStyleItem)?.Id ?? ChartStyle.South;
            var chartStyle = style == ChartStyle.North ? Controls.ChartStyle.North : Controls.ChartStyle.South;

            var entries = BirthChartCalculator.Calculate(
                date,
                (hours, minutes, 0),
                _selectedPlace.Latitude,
                _selectedPlace.Longitude,
                offsetHours,
                ayanamsaId);

            ChartListBox.ItemsSource = entries;

            _lastCalculationParams = (date, hours, minutes, _selectedPlace.Latitude, _selectedPlace.Longitude, offsetHours, ayanamsaId);

            var slotCombos = new[] { Slot0ChartTypeCombo, Slot1ChartTypeCombo, Slot2ChartTypeCombo, Slot3ChartTypeCombo };
            var slotCharts = new[] { IndianChartSlot0, IndianChartSlot1, IndianChartSlot2, IndianChartSlot3 };
            for (int i = 0; i < 4; i++)
            {
                string chartTypeId = (slotCombos[i].SelectedItem as ChartTypeItem)?.Id ?? "RasiD1";
                var chartData = BirthChartCalculator.CalculateChartData(
                    chartTypeId,
                    date,
                    (hours, minutes, 0),
                    _selectedPlace.Latitude,
                    _selectedPlace.Longitude,
                    offsetHours,
                    ayanamsaId);
                slotCharts[i].ChartStyle = chartStyle;
                slotCharts[i].Houses = chartData.Houses
                    .Select(h => new HouseCell(h.HouseNumber, h.SignName, h.Bodies))
                    .ToList();
            }

            ChartTypeCombo.SelectedItem = Slot0ChartTypeCombo.SelectedItem;

            BirthDataExpander.IsExpanded = false;
            BirthPlaceExpander.IsExpanded = false;
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

    private void SlotChartType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_lastCalculationParams is not { } p || sender is not System.Windows.Controls.ComboBox combo)
            return;
        var slotCharts = new[] { IndianChartSlot0, IndianChartSlot1, IndianChartSlot2, IndianChartSlot3 };
        var slotCombos = new[] { Slot0ChartTypeCombo, Slot1ChartTypeCombo, Slot2ChartTypeCombo, Slot3ChartTypeCombo };
        int index = -1;
        for (int i = 0; i < 4; i++)
            if (slotCombos[i] == combo) { index = i; break; }
        if (index < 0 || combo.SelectedItem is not ChartTypeItem item) return;

        var chartData = BirthChartCalculator.CalculateChartData(
            item.Id,
            p.date,
            (p.hours, p.minutes, 0),
            p.lat,
            p.lon,
            p.offsetHours,
            p.ayanamsaId);
        var style = (ChartStyleCombo.SelectedItem as ChartStyleItem)?.Id ?? ChartStyle.South;
        var chartStyle = style == ChartStyle.North ? Controls.ChartStyle.North : Controls.ChartStyle.South;
        slotCharts[index].ChartStyle = chartStyle;
        slotCharts[index].Houses = chartData.Houses
            .Select(h => new HouseCell(h.HouseNumber, h.SignName, h.Bodies))
            .ToList();
        if (index == 0)
            ChartTypeCombo.SelectedItem = combo.SelectedItem;
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        ChartListBox.ItemsSource = null;
        _lastCalculationParams = null;
        foreach (var chart in new[] { IndianChartSlot0, IndianChartSlot1, IndianChartSlot2, IndianChartSlot3 })
            chart.Houses = Array.Empty<HouseCell>();
        PlaceResultsList.SelectedItem = null;
        _selectedPlace = null;
        CoordsText.Text = "—";
    }

    private async void CopyChartImageButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ChartContainerBorder.UpdateLayout();
            if (ChartContainerBorder.ActualWidth <= 0 || ChartContainerBorder.ActualHeight <= 0)
                return;

            var width = (int)Math.Ceiling(ChartContainerBorder.ActualWidth);
            var height = (int)Math.Ceiling(ChartContainerBorder.ActualHeight);

            var rtb = new RenderTargetBitmap(
                width,
                height,
                96, 96,
                PixelFormats.Pbgra32);

            rtb.Render(ChartContainerBorder);
            Clipboard.SetImage(rtb);
            await ShowCopyStatusAsync("Chart image copied");
        }
        catch (Exception ex)
        {
            await ShowCopyStatusAsync("Copy image failed", isError: true);
            System.Diagnostics.Debug.WriteLine("Copy chart image failed: " + ex);
        }
    }

    private async void CopyChartTextButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ChartListBox.ItemsSource is not IEnumerable<BirthChartEntry> entries)
            {
                await ShowCopyStatusAsync("Create a chart first", isError: true);
                return;
            }

            var sb = new StringBuilder();
            foreach (var entry in entries)
            {
                sb.AppendLine(entry.ToString());
            }

            Clipboard.SetText(sb.ToString());
            await ShowCopyStatusAsync("Chart text copied");
        }
        catch (Exception ex)
        {
            await ShowCopyStatusAsync("Copy text failed", isError: true);
            System.Diagnostics.Debug.WriteLine("Copy chart text failed: " + ex);
        }
    }

    private async Task ShowCopyStatusAsync(string message, bool isError = false)
    {
        if (CopyStatusText == null) return;

        var original = CopyStatusText.Text;
        CopyStatusText.Text = message;
        CopyStatusText.Foreground = isError
            ? new SolidColorBrush(Color.FromRgb(200, 40, 40))
            : (Brush)FindResource("SecondaryTextBrush") ?? Brushes.Gray;

        try
        {
            await Task.Delay(2000);
        }
        catch
        {
            // ignore
        }

        if (CopyStatusText.Text == message)
        {
            CopyStatusText.Text = original;
        }
    }

    private void NewMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SwitchToSingleMode();
        ChartListBox.ItemsSource = null;
        _lastCalculationParams = null;
        foreach (var chart in new[] { IndianChartSlot0, IndianChartSlot1, IndianChartSlot2, IndianChartSlot3 })
            chart.Houses = Array.Empty<HouseCell>();
    }

    private void NewDatabaseMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _birthDatabase = new BirthDatabaseFile { Entries = new List<BirthDataFile>() };
        _currentDatabasePath = null;
        SwitchToDatabaseMode(null);
        _selectedDatabaseIndex = -1;
        ChartListBox.ItemsSource = null;
        _lastCalculationParams = null;
        foreach (var chart in new[] { IndianChartSlot0, IndianChartSlot1, IndianChartSlot2, IndianChartSlot3 })
            chart.Houses = Array.Empty<HouseCell>();
    }

    private void OpenMenuItem_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dlg = new OpenFileDialog
            {
                Title = "Open birth data",
                Filter = "Nyagrodha birth data (*.ny)|*.ny|Nyagrodha birth database (*.nydb)|*.nydb|JSON (*.json)|*.json|All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false,
            };
            if (dlg.ShowDialog(this) != true) return;

            OpenFile(dlg.FileName);
        }
        catch (System.Exception ex)
        {
            MessageBox.Show("Open failed: " + ex.Message, "Nyagrodha", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenFile(string path)
    {
        var json = File.ReadAllText(path);
        var isDbByPath = IsDatabasePath(path);
        var isDbByContent = path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && IsDatabaseFormat(json);

        if (isDbByPath || isDbByContent)
        {
            var db = JsonSerializer.Deserialize<BirthDatabaseFile>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
            if (db == null) throw new System.Exception("File is empty or invalid JSON.");
            if (db.Entries == null || db.Entries.Count == 0)
                throw new System.Exception("Birth database has no entries.");
            foreach (var entry in db.Entries)
                ValidateBirthDataFile(entry);

            _birthDatabase = db;
            SwitchToDatabaseMode(path);
            DatabaseEntriesList.SelectedIndex = 0;
            return;
        }

        // Single birth file
        var data = JsonSerializer.Deserialize<BirthDataFile>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });
        if (data == null) throw new System.Exception("File is empty or invalid JSON.");
        ValidateBirthDataFile(data);

        SwitchToSingleMode();
        ApplyBirthDataToForm(data);
        ChartListBox.ItemsSource = null;
    }

    private static void ValidateBirthDataFile(BirthDataFile data)
    {
        if (string.IsNullOrWhiteSpace(data.StdTime)) throw new System.Exception("Missing StdTime.");
        if (!TryParseStdTime(data.StdTime, out _, out _, out _, out _))
            throw new System.Exception("StdTime must be formatted like \"HH:mm dd/MM/yyyy zzz\".");
        if (data.Location is null) throw new System.Exception("Missing Location.");
        if (data.Location.Latitude is < -90 or > 90) throw new System.Exception("Invalid latitude.");
        if (data.Location.Longitude is < -180 or > 180) throw new System.Exception("Invalid longitude.");
    }

    private void ApplyBirthDataToForm(BirthDataFile data)
    {
        if (!TryParseStdTime(data.StdTime, out var date, out var hours, out var minutes, out var offsetHours))
            return;
        BirthDatePicker.SelectedDate = new DateTime(date.Year, date.Month, date.Day);
        BirthTimeBox.Text = $"{hours:D2}:{minutes:D2}";

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

        if (data.Chart != null)
        {
            ChartStyleCombo.SelectedItem = ((IEnumerable<ChartStyleItem>)ChartStyleCombo.ItemsSource)
                .FirstOrDefault(x => x.Id.ToString() == data.Chart.Style.ToString()) ?? ChartStyleCombo.SelectedItem;
            var chartTypeItem = ((IEnumerable<ChartTypeItem>)ChartTypeCombo.ItemsSource)
                .FirstOrDefault(x => x.Id == data.Chart.ChartType);
            ChartTypeCombo.SelectedItem = chartTypeItem ?? ChartTypeCombo.SelectedItem;
            Slot0ChartTypeCombo.SelectedItem = chartTypeItem ?? Slot0ChartTypeCombo.SelectedItem;
        }

        _loadedOffsetOverrideHours = offsetHours;
    }

    /// <summary>Runs chart calculation from current form values. Returns true if calculation ran.</summary>
    private bool RunChartCalculation()
    {
        if (!BirthDatePicker.SelectedDate.HasValue) return false;
        if (!TryParseTime(BirthTimeBox.Text?.Trim() ?? "", out int hours, out int minutes)) return false;
        if (_selectedPlace == null) return false;

        var date = DateOnly.FromDateTime(BirthDatePicker.SelectedDate.Value);
        var birthDateTime = new DateTime(date.Year, date.Month, date.Day, hours, minutes, 0);
        double offsetHours = _loadedOffsetOverrideHours ?? TimeZoneHelper.GetOffsetHours(
            _selectedPlace.TimeZone,
            birthDateTime,
            _selectedPlace.Longitude);
        _loadedOffsetOverrideHours = null;

        string? ayanamsaId = (AyanamsaCombo.SelectedItem as AyanamsaItem)?.Id;
        var style = (ChartStyleCombo.SelectedItem as ChartStyleItem)?.Id ?? ChartStyle.South;
        var chartStyle = style == ChartStyle.North ? Controls.ChartStyle.North : Controls.ChartStyle.South;

        var entries = BirthChartCalculator.Calculate(
            date,
            (hours, minutes, 0),
            _selectedPlace.Latitude,
            _selectedPlace.Longitude,
            offsetHours,
            ayanamsaId);
        ChartListBox.ItemsSource = entries;
        _lastCalculationParams = (date, hours, minutes, _selectedPlace.Latitude, _selectedPlace.Longitude, offsetHours, ayanamsaId);

        var slotCombos = new[] { Slot0ChartTypeCombo, Slot1ChartTypeCombo, Slot2ChartTypeCombo, Slot3ChartTypeCombo };
        var slotCharts = new[] { IndianChartSlot0, IndianChartSlot1, IndianChartSlot2, IndianChartSlot3 };
        for (int i = 0; i < 4; i++)
        {
            string chartTypeId = (slotCombos[i].SelectedItem as ChartTypeItem)?.Id ?? "RasiD1";
            var chartData = BirthChartCalculator.CalculateChartData(
                chartTypeId,
                date,
                (hours, minutes, 0),
                _selectedPlace.Latitude,
                _selectedPlace.Longitude,
                offsetHours,
                ayanamsaId);
            slotCharts[i].ChartStyle = chartStyle;
            slotCharts[i].Houses = chartData.Houses
                .Select(h => new HouseCell(h.HouseNumber, h.SignName, h.Bodies))
                .ToList();
        }
        ChartTypeCombo.SelectedItem = Slot0ChartTypeCombo.SelectedItem;
        return true;
    }

    private void DatabaseEntriesList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_appMode != AppMode.BirthDatabase || _birthDatabase == null || DatabaseEntriesList?.SelectedItem is not BirthEntryDisplay display)
            return;

        // Save current form to the previously selected entry
        if (_selectedDatabaseIndex >= 0 && _selectedDatabaseIndex < _birthDatabase.Entries.Count)
        {
            try
            {
                var current = GetCurrentBirthDataFileOrThrow();
                _birthDatabase.Entries[_selectedDatabaseIndex] = current;
                _databaseEntryDisplays[_selectedDatabaseIndex] = new BirthEntryDisplay(current);
            }
            catch
            {
                // Form invalid; don't overwrite
            }
        }

        var newIndex = _databaseEntryDisplays.IndexOf(display);
        _selectedDatabaseIndex = newIndex >= 0 ? newIndex : -1;
        if (_selectedDatabaseIndex >= 0 && _selectedDatabaseIndex < _birthDatabase.Entries.Count)
        {
            ApplyBirthDataToForm(_birthDatabase.Entries[_selectedDatabaseIndex]);
            RunChartCalculation();
        }
    }

    private void AddDatabaseEntry_Click(object sender, RoutedEventArgs e)
    {
        if (_birthDatabase == null) return;
        var defaults = new BirthDataFile
        {
            StdTime = "12:00 01/01/2000 +05:30",
            Location = new BirthDataFile.BirthLocation
            {
                Name = "New entry",
                Country = "",
                Latitude = 0,
                Longitude = 0,
                TimeZone = "UTC",
            },
        };
        _birthDatabase.Entries.Add(defaults);
        _databaseEntryDisplays.Add(new BirthEntryDisplay(defaults));
        DatabaseEntriesList.SelectedIndex = _databaseEntryDisplays.Count - 1;
    }

    private void RemoveDatabaseEntry_Click(object sender, RoutedEventArgs e)
    {
        if (_birthDatabase == null || _selectedDatabaseIndex < 0 || _selectedDatabaseIndex >= _birthDatabase.Entries.Count)
            return;
        _birthDatabase.Entries.RemoveAt(_selectedDatabaseIndex);
        _databaseEntryDisplays.RemoveAt(_selectedDatabaseIndex);
        _selectedDatabaseIndex = -1;
        if (_birthDatabase.Entries.Count > 0)
        {
            DatabaseEntriesList.SelectedIndex = Math.Min(_databaseEntryDisplays.Count - 1, 0);
        }
        else
        {
            ChartListBox.ItemsSource = null;
            foreach (var chart in new[] { IndianChartSlot0, IndianChartSlot1, IndianChartSlot2, IndianChartSlot3 })
                chart.Houses = Array.Empty<HouseCell>();
        }
    }

    private void SaveMenuItem_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_appMode == AppMode.BirthDatabase && _birthDatabase != null)
            {
                SyncCurrentFormToSelectedEntry();
                var path = _currentDatabasePath;
                if (string.IsNullOrEmpty(path))
                {
                    var dlg = new SaveFileDialog
                    {
                        Title = "Save birth database",
                        Filter = "Nyagrodha birth database (*.nydb)|*.nydb|All files (*.*)|*.*",
                        AddExtension = true,
                        DefaultExt = ".nydb",
                        FileName = "birth-database.nydb",
                        OverwritePrompt = true,
                    };
                    if (dlg.ShowDialog(this) != true) return;
                    path = dlg.FileName;
                }
                var json = JsonSerializer.Serialize(_birthDatabase, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
                _currentDatabasePath = path;
                UpdateWindowTitle(path);
                return;
            }

            var current = GetCurrentBirthDataFileOrThrow();
            var dlgSingle = new SaveFileDialog
            {
                Title = "Save birth data",
                Filter = "Nyagrodha birth data (*.ny)|*.ny|All files (*.*)|*.*",
                AddExtension = true,
                DefaultExt = ".ny",
                FileName = "birth-data.ny",
                OverwritePrompt = true,
            };
            if (dlgSingle.ShowDialog(this) != true) return;

            var jsonSingle = JsonSerializer.Serialize(current, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dlgSingle.FileName, jsonSingle);
        }
        catch (System.Exception ex)
        {
            MessageBox.Show("Save failed: " + ex.Message, "Nyagrodha", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveAsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_appMode == AppMode.BirthDatabase && _birthDatabase != null)
            {
                var dlg = new SaveFileDialog
                {
                    Title = "Save As",
                    Filter = "Nyagrodha birth data (*.ny)|*.ny|Nyagrodha birth database (*.nydb)|*.nydb|All files (*.*)|*.*",
                    AddExtension = true,
                    DefaultExt = ".nydb",
                    FileName = "birth-database.nydb",
                    OverwritePrompt = true,
                };
                if (dlg.ShowDialog(this) != true) return;
                SyncCurrentFormToSelectedEntry();
                var ext = System.IO.Path.GetExtension(dlg.FileName);
                if (string.Equals(ext, ".nydb", StringComparison.OrdinalIgnoreCase))
                {
                    var json = JsonSerializer.Serialize(_birthDatabase, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(dlg.FileName, json);
                    _currentDatabasePath = dlg.FileName;
                    UpdateWindowTitle(dlg.FileName);
                }
                else
                {
                    var one = _selectedDatabaseIndex >= 0 && _selectedDatabaseIndex < _birthDatabase.Entries.Count
                        ? _birthDatabase.Entries[_selectedDatabaseIndex]
                        : GetCurrentBirthDataFileOrThrow();
                    var json = JsonSerializer.Serialize(one, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(dlg.FileName, json);
                }
                return;
            }

            var current = GetCurrentBirthDataFileOrThrow();
            var dlgSingle = new SaveFileDialog
            {
                Title = "Save As",
                Filter = "Nyagrodha birth data (*.ny)|*.ny|Nyagrodha birth database (*.nydb)|*.nydb|All files (*.*)|*.*",
                AddExtension = true,
                DefaultExt = ".ny",
                FileName = "birth-data.ny",
                OverwritePrompt = true,
            };
            if (dlgSingle.ShowDialog(this) != true) return;
            var extSingle = System.IO.Path.GetExtension(dlgSingle.FileName);
            if (string.Equals(extSingle, ".nydb", StringComparison.OrdinalIgnoreCase))
            {
                var db = new BirthDatabaseFile { Entries = new List<BirthDataFile> { current } };
                var json = JsonSerializer.Serialize(db, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dlgSingle.FileName, json);
            }
            else
            {
                var json = JsonSerializer.Serialize(current, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dlgSingle.FileName, json);
            }
        }
        catch (System.Exception ex)
        {
            MessageBox.Show("Save As failed: " + ex.Message, "Nyagrodha", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SyncCurrentFormToSelectedEntry()
    {
        if (_appMode != AppMode.BirthDatabase || _birthDatabase == null || _selectedDatabaseIndex < 0 || _selectedDatabaseIndex >= _birthDatabase.Entries.Count)
            return;
        try
        {
            var current = GetCurrentBirthDataFileOrThrow();
            _birthDatabase.Entries[_selectedDatabaseIndex] = current;
            _databaseEntryDisplays[_selectedDatabaseIndex] = new BirthEntryDisplay(current);
        }
        catch { /* form invalid */ }
    }

    private void KeyboardShortcutsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var chartTypeLines = string.Join("\n", ChartTypeItem.All.Take(9).Select((item, i) =>
            $"  Ctrl+Alt+{i + 1}  {item.DisplayText}"));
        var msg = "Create chart\tCtrl+Enter\n" +
                  "Clear\tCtrl+Shift+C or Escape\n" +
                  "Copy chart image\tCtrl+Shift+I\n" +
                  "Copy chart as text\tCtrl+Shift+T\n" +
                  "Chart style: South\tCtrl+1\n" +
                  "Chart style: North\tCtrl+2\n" +
                  "Chart type (first 9):\n" + chartTypeLines + "\n" +
                  "Ayanamsa dropdown\tAlt+A";
        MessageBox.Show(msg, "Keyboard shortcuts", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e) => Close();

    private sealed class PlaceDisplay
    {
        public Place Place { get; }
        public string DisplayText => $"{Place.Name}, {Place.Country}";
        public PlaceDisplay(Place place) => Place = place;
    }

    /// <summary>Display wrapper for a birth database entry in the list.</summary>
    private sealed class BirthEntryDisplay
    {
        public BirthDataFile Entry { get; }
        public string DisplayText => !string.IsNullOrWhiteSpace(Entry.Name)
            ? Entry.Name
            : $"{Entry.StdTime} — {Entry.Location?.Name ?? "?"}";
        public BirthEntryDisplay(BirthDataFile entry) => Entry = entry;
    }

    private static bool IsDatabasePath(string path)
    {
        return path.EndsWith(".nydb", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDatabaseFormat(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("Entries", out var entries) && entries.ValueKind == JsonValueKind.Array;
        }
        catch
        {
            return false;
        }
    }

    private void SwitchToSingleMode()
    {
        _appMode = AppMode.SingleBirth;
        _birthDatabase = null;
        _currentDatabasePath = null;
        _selectedDatabaseIndex = -1;
        _databaseEntryDisplays.Clear();
        if (DatabaseEntriesList != null)
            DatabaseEntriesList.ItemsSource = null;
        if (DatabaseModePanel != null)
            DatabaseModePanel.Visibility = Visibility.Collapsed;
        UpdateWindowTitle(null);
    }

    private void SwitchToDatabaseMode(string? path)
    {
        _appMode = AppMode.BirthDatabase;
        _currentDatabasePath = path;
        _databaseEntryDisplays.Clear();
        if (_birthDatabase != null)
        {
            foreach (var e in _birthDatabase.Entries)
                _databaseEntryDisplays.Add(new BirthEntryDisplay(e));
        }
        if (DatabaseModePanel != null)
            DatabaseModePanel.Visibility = Visibility.Visible;
        if (DatabaseEntriesList != null)
            DatabaseEntriesList.ItemsSource = _databaseEntryDisplays;
        UpdateWindowTitle(path);
    }

    private void UpdateWindowTitle(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Title = "Nyagrodha";
            return;
        }
        Title = "Nyagrodha — " + System.IO.Path.GetFileName(filePath);
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
        var chartType = (Slot0ChartTypeCombo.SelectedItem as ChartTypeItem)?.Id ?? "RasiD1";

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
