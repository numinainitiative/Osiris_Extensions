param([ValidateSet("Debug","Release")][string]$Configuration = "Release", [switch]$Force)
$ErrorActionPreference = "Stop"
& (Join-Path $PSScriptRoot "..\..\build\New-OsirisExtensionPackage.ps1") `
    -ExtensionName "ScreenshotsGallery" `
    -BuildScript (Join-Path $PSScriptRoot "build.ps1") `
    -BuildOutput (Join-Path $PSScriptRoot "source\bin\$Configuration\net462") `
    -LicensePath (Join-Path $PSScriptRoot "LICENSE") `
    -AdditionalLicenseFiles @{ "Lucide-License.txt" = (Join-Path $PSScriptRoot "LICENSES\Lucide.txt") } `
    -Configuration $Configuration -Force:$Force
