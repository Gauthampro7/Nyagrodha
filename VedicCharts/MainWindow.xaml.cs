using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
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
            MessageBox.Show("Please select a birth date.", "Vedic Charts", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var date = DateOnly.FromDateTime(BirthDatePicker.SelectedDate.Value);

        if (!TryParseTime(BirthTimeBox.Text?.Trim() ?? "", out int hours, out int minutes))
        {
            MessageBox.Show("Please enter birth time as HH:mm (24-hour).", "Vedic Charts", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_selectedPlace == null)
        {
            MessageBox.Show("Please search and select a birth place.", "Vedic Charts", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var birthDateTime = new System.DateTime(date.Year, date.Month, date.Day, hours, minutes, 0);
            double offsetHours = TimeZoneHelper.GetOffsetHours(
                _selectedPlace.TimeZone,
                birthDateTime,
                _selectedPlace.Longitude);

            string? ayanamsaId = (AyanamsaCombo.SelectedItem as AyanamsaItem)?.Id;

            var entries = BirthChartCalculator.Calculate(
                date,
                (hours, minutes, 0),
                _selectedPlace.Latitude,
                _selectedPlace.Longitude,
                offsetHours,
                ayanamsaId);

            ChartListBox.ItemsSource = entries;
        }
        catch (System.Exception ex)
        {
            MessageBox.Show("Calculation failed: " + ex.Message, "Vedic Charts", MessageBoxButton.OK, MessageBoxImage.Error);
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
        PlaceResultsList.SelectedItem = null;
        _selectedPlace = null;
        CoordsText.Text = "—";
    }

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
}
