using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VedicCharts.Core;

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
        var houses = Houses ?? Array.Empty<HouseCell>();
        View = ChartStyle == ChartStyle.North
            ? NorthChartView.FromHouses(houses)
            : SouthChartView.FromHouses(houses);
    }

    internal static string FormatBodies(HouseCell house)
    {
        if (house.Bodies.Count == 0)
            return house.HouseNumber == 1 ? "Lg" : string.Empty;

        var parts = house.Bodies
            .Select(b => $"{b.ShortName} {FormatDegree(b.DegreeInSign)}");

        var core = string.Join("  ", parts);
        return house.HouseNumber == 1 ? $"Lg  {core}" : core;
    }

    private static string FormatDegree(double degreeInSign)
    {
        // Normalize and format as DD°MM'
        var d = (int)Math.Floor(degreeInSign);
        var minutesRaw = (degreeInSign - d) * 60.0;
        var m = (int)Math.Round(minutesRaw);
        if (m >= 60)
        {
            m -= 60;
            d += 1;
        }
        d = ((d % 30) + 30) % 30;
        return $"{d:00}°{m:00}'";
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
    IReadOnlyList<VedicChartBody> Bodies);

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
                PlanetsText = IndianChartControl.FormatBodies(house),
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
        // North Indian chart is sign-fixed like the South chart.
        // We place signs in a conventional diamond layout and then
        // attach the corresponding house/planets for each sign.
        var signOrder = new[]
        {
            "Aries","Taurus","Gemini","Cancer",
            "Leo","Virgo","Libra","Scorpio",
            "Sagittarius","Capricorn","Aquarius","Pisces"
        };

        // Approximate positions for each sign on a 420x420 canvas.
        var positions = new Dictionary<string, (double X, double Y)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Aries"] = (210, 20),        // top
            ["Taurus"] = (310, 80),       // upper-right
            ["Gemini"] = (360, 210),      // right
            ["Cancer"] = (310, 330),      // lower-right
            ["Leo"] = (210, 380),         // bottom
            ["Virgo"] = (110, 330),       // lower-left
            ["Libra"] = (60, 210),        // left
            ["Scorpio"] = (110, 80),      // upper-left
            ["Sagittarius"] = (210, 115), // inner top
            ["Capricorn"] = (275, 210),   // inner right
            ["Aquarius"] = (210, 305),    // inner bottom
            ["Pisces"] = (145, 210),      // inner left
        };

        var bySign = houses
            .GroupBy(h => h.SignName)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var view = new NorthChartView();
        foreach (var sign in signOrder)
        {
            if (!positions.TryGetValue(sign, out var pos)) continue;
            bySign.TryGetValue(sign, out var house);

            var labelHouse = house?.HouseNumber.ToString() ?? "";
            var planetsText = house is null ? "" : IndianChartControl.FormatBodies(house);

            view.Cells.Add(new NorthCellVm
            {
                X = pos.X,
                Y = pos.Y,
                HouseLabel = string.IsNullOrEmpty(labelHouse) ? "" : $"H{labelHouse}",
                SignLabel = ShortSign(sign),
                PlanetsText = planetsText,
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

