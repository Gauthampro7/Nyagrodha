# Nyagrodha (Vedic Astrology Charts)

**Nyagrodha** is a desktop application for generating and viewing Vedic (Jyotish) birth charts. It supports North Indian and South Indian chart styles and multiple divisional charts (vargas).

> **Note:** Development is aggressively ongoing. There may be bugs; use at your own discretion and please report issues if you find them.

## Features

- **Birth chart calculation** using Swiss Ephemeris (SwissEphNet) with Lahiri ayanamsa
- **Chart styles**: North Indian (house-based) and South Indian (sign-based) layouts
- **16 vargas**: Rasi (D1), Hora (D2), Drekkana (D3), Chaturthamsha (D4), Saptamsha (D7), Navamsha (D9), and others through D60
- **Save / Open**: Store birth data in `.ny` files (JSON) for reuse
- **WPF desktop app** (Windows) with native-rendered chart controls

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows (for the WPF app)

## Building and running

From the repository root:

```bash
dotnet build VedicCharts.slnx
dotnet run --project VedicCharts
```

Or open `VedicCharts.slnx` in Visual Studio / Rider and run the **VedicCharts** (Nyagrodha) project.

## Solution structure

| Project            | Description |
|--------------------|-------------|
| **VedicCharts**    | Nyagrodha WPF app — main desktop UI |
| **VedicCharts.Core** | Shared library: chart math, vargas, Swiss Ephemeris, VedAstro.Library integration |
| **VedicCharts.Tests** | Unit tests for core logic |

The solution file is `VedicCharts.slnx` (SDK-style solution). It references only the Core and WPF app; add **VedicCharts.Tests** to the solution if you want to run tests from the IDE.

## Birth data format

Saved `.ny` files are JSON. They store person name, birth date/time, and place (with timezone), in a shape compatible with the core calculator and the MAUI/Blazor front ends in **VedAstroRepo** (if used).

## Other folders

- **VedAstroRepo** — Separate solution (API, MAUI Desktop, Website, etc.); may use the same birth-data format.
- **ReflectVedAstro** — Blazor/desktop experiment.
- **vedastro-extract** — Supporting or extracted assets.

## License

See repository or project files for license terms.
