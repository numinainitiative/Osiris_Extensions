param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "source\Osiris.SteamGridDBMetadata.csproj"
dotnet build $project -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "SteamGridDB Metadata build failed with exit code $LASTEXITCODE."
}

$output = Join-Path $PSScriptRoot "source\bin\$Configuration\net462"
Write-Host "SteamGridDB Metadata build complete: $output"
