param(
    [string]$SourceGame = 'C:/Users/user/Documents/GitHub/rintrint.github.io/gamejam',
    [string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent)
)
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $ProjectRoot 'Assets/Scripts/Core/DifficultyBalance.cs'), (Join-Path $ProjectRoot 'Assets/Scripts/Core/GuguRun.cs'), (Join-Path $ProjectRoot 'Assets/Scripts/Core/CoreParity.cs')
$options = [System.Text.Json.JsonSerializerOptions]::new()
$options.IncludeFields = $true
$chartPath = Join-Path $SourceGame 'assets/floe/chart.json'
if (!(Test-Path -LiteralPath $chartPath)) {
    $chartPath = Join-Path $ProjectRoot 'Assets/Resources/Data/chart.json'
}
$charts = [System.Text.Json.JsonSerializer]::Deserialize([System.IO.File]::ReadAllText($chartPath), [SealGugu.ChartDocument], $options)
$golden = [System.Text.Json.JsonSerializer]::Deserialize([System.IO.File]::ReadAllText((Join-Path $PSScriptRoot 'core-golden.json')), [SealGugu.GoldenSuite], $options)
[SealGugu.CoreParity]::Run($charts, $golden)
