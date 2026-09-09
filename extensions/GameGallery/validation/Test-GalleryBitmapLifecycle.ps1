param(
    [Parameter(Mandatory = $true)]
    [string]$ImageRoot,

    [int]$Cycles = 100
)

$ErrorActionPreference = 'Stop'
if ([Environment]::Is64BitProcess) {
    throw 'Run this validation with 32-bit Windows PowerShell.'
}

$sourceRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = Join-Path $sourceRoot 'source\bin\Release'
Get-ChildItem -LiteralPath $outputRoot -Filter '*.dll' | ForEach-Object {
    try { [void][Reflection.Assembly]::LoadFrom($_.FullName) } catch { }
}

$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $outputRoot 'GameGallery.dll'))
$providerType = $assembly.GetType('SteamScreenshots.Infrastructure.Providers.UrlImageProvider', $true)
$screenshotType = $assembly.GetType('SteamScreenshots.Domain.ValueObjects.Screenshot', $true)
$providerConstructor = $providerType.GetConstructors() | Select-Object -First 1
$cachePath = [string](Join-Path $env:TEMP 'OsirisGalleryLifecycle')
$provider = $providerConstructor.Invoke([object[]]@($cachePath, $null))
$constructor = $screenshotType.GetConstructors() | Select-Object -First 1
$getStage = $screenshotType.GetMethod('GetStageImage')
$releaseStage = $screenshotType.GetMethod('ReleaseStageImage')

$images = @(Get-ChildItem -LiteralPath $ImageRoot -Recurse -File |
    Where-Object { $_.Extension -match '^\.(jpg|jpeg|png|webp)$' -and $_.Length -gt 256KB } |
    Sort-Object Length -Descending |
    Select-Object -First 24)
if ($images.Count -lt 5) {
    throw "At least five real gallery images are required under $ImageRoot."
}

function Get-PrivateMb {
    [Math]::Round((Get-Process -Id $PID).PrivateMemorySize64 / 1MB, 1)
}

[GC]::Collect()
[GC]::WaitForPendingFinalizers()
[GC]::Collect()
$baseline = Get-PrivateMb
$peak = $baseline
$previous = $null

for ($index = 0; $index -lt $Cycles; $index++) {
    $path = [string]$images[$index % $images.Count].FullName
    $screenshot = $constructor.Invoke([object[]]@($path, $path, $provider))
    $bitmap = $getStage.Invoke($screenshot, @([Threading.CancellationToken]::None))
    if ($bitmap.PixelWidth -gt 1280 -or $bitmap.PixelHeight -gt 720) {
        throw "Stage decode exceeded its ceiling: $($bitmap.PixelWidth)x$($bitmap.PixelHeight)."
    }

    if ($null -ne $previous) {
        [void]$releaseStage.Invoke($previous, @())
    }
    $previous = $screenshot
    $bitmap = $null
    $screenshot = $null
    if (($index % 4) -eq 3) {
        Start-Sleep -Milliseconds 125
    }
    $peak = [Math]::Max($peak, (Get-PrivateMb))
}

if ($null -ne $previous) {
    [void]$releaseStage.Invoke($previous, @())
}
$previous = $null
Start-Sleep -Milliseconds 1200
[GC]::Collect()
[GC]::WaitForPendingFinalizers()
[GC]::Collect()
$final = Get-PrivateMb

[pscustomobject]@{
    Architecture = 'x86'
    Cycles = $Cycles
    UniqueImages = $images.Count
    BaselinePrivateMb = $baseline
    PeakPrivateMb = $peak
    FinalPrivateMb = $final
    RetainedPrivateMb = [Math]::Round($final - $baseline, 1)
    Result = if (($final - $baseline) -le 12) { 'PASS' } else { 'FAIL' }
}

if (($final - $baseline) -gt 12) {
    exit 1
}
