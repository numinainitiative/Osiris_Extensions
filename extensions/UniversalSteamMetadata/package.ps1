[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$Force
)

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$additionalLicenses = @{
    "SteamKit2-NOTICE.txt" = Join-Path $PSScriptRoot "LICENSES\SteamKit2-NOTICE.txt"
    "LGPL-2.1.txt" = Join-Path $PSScriptRoot "LICENSES\LGPL-2.1.txt"
}
& (Join-Path $repositoryRoot "build\New-OsirisExtensionPackage.ps1") `
    -ExtensionName "SteamMetadata" `
    -BuildScript (Join-Path $PSScriptRoot "build.ps1") `
    -BuildOutput (Join-Path $PSScriptRoot "source\bin\$Configuration\net462") `
    -LicensePath (Join-Path $PSScriptRoot "LICENSE") `
    -AdditionalLicenseFiles $additionalLicenses `
    -Configuration $Configuration `
    -Force:$Force
