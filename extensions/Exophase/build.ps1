param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "source\Osiris.Exophase.csproj"
$validation = Join-Path $PSScriptRoot "validation\Osiris.Exophase.Validation.csproj"

dotnet build $project -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Exophase build failed with exit code $LASTEXITCODE."
}

dotnet run --project $validation -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Exophase validation failed with exit code $LASTEXITCODE."
}

$output = Join-Path $PSScriptRoot "source\bin\$Configuration\net462"
Write-Host "Exophase build complete: $output"
