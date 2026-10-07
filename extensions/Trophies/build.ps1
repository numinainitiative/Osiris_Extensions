param([ValidateSet("Debug","Release")][string]$Configuration = "Release")
$ErrorActionPreference = "Stop"
dotnet build (Join-Path $PSScriptRoot 'source\Osiris.Trophies.csproj') -c $Configuration
if ($LASTEXITCODE -ne 0) { throw 'Trophies build failed.' }
& powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Build-Icon.ps1') -OutputDirectory (Join-Path $PSScriptRoot "source\bin\$Configuration\net462")
if ($LASTEXITCODE -ne 0) { throw 'Trophies icon generation failed.' }
dotnet run --project (Join-Path $PSScriptRoot 'tests\Trophies.Validation.csproj') -c $Configuration
if ($LASTEXITCODE -ne 0) { throw 'Trophies validation failed.' }
