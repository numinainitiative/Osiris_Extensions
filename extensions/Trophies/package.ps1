param([ValidateSet("Debug","Release")][string]$Configuration = "Release", [switch]$Force)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot '..\..\build\New-OsirisExtensionPackage.ps1') `
 -ExtensionName 'Trophies' `
 -BuildScript (Join-Path $PSScriptRoot 'build.ps1') `
 -BuildOutput (Join-Path $PSScriptRoot "source\bin\$Configuration\net462") `
 -LicensePath (Join-Path $PSScriptRoot 'LICENSE') `
 -Configuration $Configuration -Force:$Force
