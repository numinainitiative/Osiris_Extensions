[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$Force
)

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
& (Join-Path $repositoryRoot "build\New-OsirisExtensionPackage.ps1") `
    -ExtensionName "IgnMetadata" `
    -BuildScript (Join-Path $PSScriptRoot "build.ps1") `
    -BuildOutput (Join-Path $PSScriptRoot "source\IgnMetadata\bin\$Configuration\net462") `
    -LicensePath (Join-Path $PSScriptRoot "LICENSE") `
    -Configuration $Configuration `
    -Force:$Force
