param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "source\XboxLibrary.csproj"
dotnet build $project -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Xbox Library build failed with exit code $LASTEXITCODE."
}

$output = Join-Path $PSScriptRoot "source\bin\$Configuration\net462"
Write-Host "Xbox Library build complete: $output"
