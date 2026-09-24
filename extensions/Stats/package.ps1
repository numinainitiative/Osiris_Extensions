param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$extensionRoot = $PSScriptRoot
$packager = Join-Path $extensionRoot "..\..\build\New-OsirisExtensionPackage.ps1"

& $packager `
    -ExtensionName "Stats" `
    -BuildScript (Join-Path $extensionRoot "build.ps1") `
    -BuildOutput (Join-Path $extensionRoot "source\bin\$Configuration\net462") `
    -LicensePath (Join-Path $extensionRoot "LICENSE") `
    -Configuration $Configuration `
    -Force:$Force
