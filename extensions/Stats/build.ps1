param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$iconPath = Join-Path $PSScriptRoot "source\icon.png"
if (-not (Test-Path -LiteralPath $iconPath -PathType Leaf)) {
    throw "The supplied Stats icon is missing: $iconPath"
}

$project = Join-Path $PSScriptRoot "source\Osiris.Stats.csproj"
dotnet build $project -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Stats build failed with exit code $LASTEXITCODE."
}

$output = Join-Path $PSScriptRoot "source\bin\$Configuration\net462"
Write-Host "Stats build complete: $output"
