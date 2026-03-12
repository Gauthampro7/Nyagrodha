param(
    [string]$OutputPath
)

if (-not $OutputPath -or [string]::IsNullOrWhiteSpace($OutputPath)) {
    # Default: next to the running Nyagrodha executable (Debug build path)
    $outputDir = Join-Path $PSScriptRoot "..\VedicCharts\bin\Debug\net8.0"
    if (-not (Test-Path $outputDir)) {
        New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    }
    $OutputPath = Join-Path $outputDir "world_cities_nyagrodha.csv"
}

$sourceUrl = "https://raw.githubusercontent.com/dr5hn/countries-states-cities-database/master/csv/cities.csv"
$tempFile = Join-Path $PSScriptRoot "cities_raw.csv"

Write-Host "Downloading world cities CSV..."
Invoke-WebRequest -Uri $sourceUrl -OutFile $tempFile -UseBasicParsing

Write-Host "Transforming to Nyagrodha format..."
$rows = Import-Csv $tempFile

$rows |
    Select-Object `
        @{Name = 'Name'; Expression = { $_.name }}, `
        @{Name = 'Country'; Expression = { $_.country_name }}, `
        @{Name = 'Latitude'; Expression = { $_.latitude }}, `
        @{Name = 'Longitude'; Expression = { $_.longitude }}, `
        @{Name = 'TimeZone'; Expression = { $_.timezone } } |
    Export-Csv -Path $OutputPath -NoTypeInformation

Remove-Item $tempFile -ErrorAction SilentlyContinue

Write-Host "Done. Generated world_cities_nyagrodha.csv at:"
Write-Host "  $OutputPath"

