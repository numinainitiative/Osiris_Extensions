param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "source\Osiris.HowLongToBeat.csproj"
dotnet build $project -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "HowLongToBeat build failed with exit code $LASTEXITCODE."
}

$output = Join-Path $PSScriptRoot "source\bin\$Configuration\net462"
Write-Host "HowLongToBeat build complete: $output"
