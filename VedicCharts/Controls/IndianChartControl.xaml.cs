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

    /// <summary>
    /// North Indian chart: house positions are FIXED. Layout matches classic North Indian diamond:
    /// Outer diamond + cross + inner square + 8 diagonals from outer corners to inner corners = 12 compartments.
    /// H1 top triangle, H2 top-left, H3 left diamond, H4 bottom-left, H5 bottom triangle, H6 bottom-right,
    /// H7 right diamond, H8 top-right, H9–H12 the four inner diamonds. Counter-clockwise from top.
    /// </summary>
    public static NorthChartView FromHouses(IReadOnlyList<HouseCell> houses)
    {
        // Box size 120×72; position = (Canvas.Left, Canvas.Top) = (centerX - 60, centerY - 36).
        // Centers chosen so each box sits inside its compartment (outer triangles, side trapezoids, inner triangles).
        var positionByHouse = new Dictionary<int, (double X, double Y)>
        {
            [1] = (150, 59),   // top centre triangle (kendra)
            [2] = (100, 94),   // top-left trapezoid
            [3] = (20, 174),   // left trapezoid (kendra)
            [4] = (100, 254),  // bottom-left trapezoid
            [5] = (150, 274),  // bottom centre triangle (kendra)
            [6] = (200, 254),  // bottom-right trapezoid
            [7] = (280, 174),   // right trapezoid (kendra)
            [8] = (200, 94),   // top-right trapezoid
            [9] = (87, 137),   // inner top-left diamond
            [10] = (213, 137),  // inner top-right diamond
            [11] = (213, 211),  // inner bottom-right diamond
            [12] = (87, 211),   // inner bottom-left diamond
        };

        var view = new NorthChartView();
        for (int houseNum = 1; houseNum <= 12; houseNum++)
        {
            var house = houses?.FirstOrDefault(h => h.HouseNumber == houseNum);
            if (!positionByHouse.TryGetValue(houseNum, out var pos))
                continue;

            view.Cells.Add(new NorthCellVm
            {
                X = pos.X,
                Y = pos.Y,
                HouseLabel = $"H{houseNum}",
                SignLabel = house != null ? ShortSign(house.SignName) : "",
                PlanetsText = house != null ? IndianChartControl.FormatBodies(house) : "",
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

