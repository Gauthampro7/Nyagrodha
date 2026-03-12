using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace VedicCharts.Controls;

public partial class IndianChartControl : UserControl
{
    public IndianChartControl()
    {
        InitializeComponent();
        UpdateView();
    }

    public ChartStyle ChartStyle
    {
        get => (ChartStyle)GetValue(ChartStyleProperty);
        set => SetValue(ChartStyleProperty, value);
    }

    public static readonly DependencyProperty ChartStyleProperty =
        DependencyProperty.Register(nameof(ChartStyle), typeof(ChartStyle), typeof(IndianChartControl),
            new PropertyMetadata(ChartStyle.South, OnAnyChanged));

    public IReadOnlyList<HouseCell> Houses
    {
        get => (IReadOnlyList<HouseCell>)GetValue(HousesProperty);
        set => SetValue(HousesProperty, value);
    }

    public static readonly DependencyProperty HousesProperty =
        DependencyProperty.Register(nameof(Houses), typeof(IReadOnlyList<HouseCell>), typeof(IndianChartControl),
            new PropertyMetadata(null, OnAnyChanged));

    public object? View
    {
        get => GetValue(ViewProperty);
        private set => SetValue(ViewPropertyKey, value);
    }

    private static readonly DependencyPropertyKey ViewPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(View), typeof(object), typeof(IndianChartControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ViewProperty = ViewPropertyKey.DependencyProperty;

    private static void OnAnyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is IndianChartControl c) c.UpdateView();
    }

    private void UpdateView()
    {
        var houses = Houses ?? [];
        View = ChartStyle == ChartStyle.North
            ? NorthChartView.FromHouses(houses)
            : SouthChartView.FromHouses(houses);
    }
}

public enum ChartStyle
{
    South,
    North,
}

public sealed record HouseCell(
    int HouseNumber,
    string SignName,
    IReadOnlyList<string> Planets);

internal sealed class SouthChartView
{
    public ObservableCollection<SouthCellVm> Cells { get; } = new();

    public static SouthChartView FromHouses(IReadOnlyList<HouseCell> houses)
    {
        // South Indian chart is sign-fixed. We render all 12 signs into a 4x4 grid with 4 empty corners.
        // Layout (row-major 4x4) uses the common South chart placement:
        // [ Pisces, Aries, Taurus, Gemini ]
        // [ Aquarius,  -,    -,    Cancer ]
        // [ Capricorn, -,    -,    Leo ]
        // [ Sagittarius, Scorpio, Libra, Virgo ]
        var signOrder = new[]
        {
            "Pisces","Aries","Taurus","Gemini",
            "Aquarius",null,null,"Cancer",
            "Capricorn",null,null,"Leo",
            "Sagittarius","Scorpio","Libra","Virgo"
        };

        var bySign = houses
            .GroupBy(h => h.SignName)
            .ToDictionary(g => g.Key, g => g.First());

        var view = new SouthChartView();
        foreach (var sign in signOrder)
        {
            if (sign == null)
            {
                view.Cells.Add(SouthCellVm.Empty());
                continue;
            }

            if (!bySign.TryGetValue(sign, out var house))
            {
                view.Cells.Add(new SouthCellVm
                {
                    SignLabel = ShortSign(sign),
                    HouseLabel = "",
                    PlanetsText = "",
                });
                continue;
            }

            view.Cells.Add(new SouthCellVm
            {
                SignLabel = ShortSign(sign),
                HouseLabel = $"H{house.HouseNumber}",
                PlanetsText = house.HouseNumber == 1 ? "Lg " + string.Join(" ", house.Planets) : string.Join(" ", house.Planets),
            });
        }

        return view;
    }

    private static string ShortSign(string sign) => sign switch
    {
        "Aries" => "Ar",
        "Taurus" => "Ta",
        "Gemini" => "Ge",
        "Cancer" => "Cn",
        "Leo" => "Le",
        "Virgo" => "Vi",
        "Libra" => "Li",
        "Scorpio" => "Sc",
        "Sagittarius" => "Sg",
        "Capricorn" => "Cp",
        "Aquarius" => "Aq",
        "Pisces" => "Pi",
        _ => sign
    };
}

internal sealed class SouthCellVm
{
    public string SignLabel { get; init; } = "";
    public string HouseLabel { get; init; } = "";
    public string PlanetsText { get; init; } = "";

    public bool IsEmpty { get; init; }

    public static SouthCellVm Empty() => new() { IsEmpty = true, SignLabel = "", HouseLabel = "", PlanetsText = "" };
}

internal sealed class NorthChartView
{
    public ObservableCollection<NorthCellVm> Cells { get; } = new();

    private static string ShortSign(string sign) => sign switch
    {
        "Aries" => "Ar",
        "Taurus" => "Ta",
        "Gemini" => "Ge",
        "Cancer" => "Cn",
        "Leo" => "Le",
        "Virgo" => "Vi",
        "Libra" => "Li",
        "Scorpio" => "Sc",
        "Sagittarius" => "Sg",
        "Capricorn" => "Cp",
        "Aquarius" => "Aq",
        "Pisces" => "Pi",
        _ => sign
    };

    public static NorthChartView FromHouses(IReadOnlyList<HouseCell> houses)
    {
        // House placement positions (approx) on a 420x420 canvas.
        // These are tuned for readability; the diamond frame is drawn separately.
        var positions = new Dictionary<int, (double X, double Y)>
        {
            [1] = (150, 20),
            [2] = (270, 70),
            [3] = (300, 175),
            [4] = (270, 280),
            [5] = (150, 330),
            [6] = (30, 280),
            [7] = (0, 175),
            [8] = (30, 70),
            [9] = (150, 110),
            [10] = (220, 175),
            [11] = (150, 240),
            [12] = (80, 175),
        };

        var view = new NorthChartView();
        foreach (var house in houses.OrderBy(h => h.HouseNumber))
        {
            if (!positions.TryGetValue(house.HouseNumber, out var pos)) continue;

            view.Cells.Add(new NorthCellVm
            {
                X = pos.X,
                Y = pos.Y,
                HouseLabel = $"H{house.HouseNumber}",
                SignLabel = ShortSign(house.SignName),
                PlanetsText = house.HouseNumber == 1 ? "Lg " + string.Join(" ", house.Planets) : string.Join(" ", house.Planets),
            });
        }

        return view;
    }
}

internal sealed class NorthCellVm
{
    public double X { get; init; }
    public double Y { get; init; }
    public string HouseLabel { get; init; } = "";
    public string SignLabel { get; init; } = "";
    public string PlanetsText { get; init; } = "";
}

