[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "source\IgnMetadata\IgnMetadata.csproj"

dotnet build $project -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "IGN Metadata build failed with exit code $LASTEXITCODE."
}

$output = Join-Path $PSScriptRoot "source\IgnMetadata\bin\$Configuration\net462"
$appOwnedDependencies = @("Newtonsoft.Json.dll")
foreach ($dependency in $appOwnedDependencies) {
    $dependencyPath = Join-Path $output $dependency
    if (Test-Path -LiteralPath $dependencyPath -PathType Leaf) {
        Remove-Item -LiteralPath $dependencyPath -Force
    }
}

Write-Host "IGN Metadata build complete: $output"
