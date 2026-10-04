param([ValidateSet("Debug","Release")][string]$Configuration = "Release")
$ErrorActionPreference = "Stop"
dotnet build (Join-Path $PSScriptRoot "source\Osiris.ScreenshotsGallery.csproj") -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Screenshots Gallery build failed." }
