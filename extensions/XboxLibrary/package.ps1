[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$Force
)

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
& (Join-Path $repositoryRoot "build\New-OsirisExtensionPackage.ps1") `
    -ExtensionName "XboxLibrary" `
    -BuildScript (Join-Path $PSScriptRoot "build.ps1") `
    -BuildOutput (Join-Path $PSScriptRoot "source\bin\$Configuration\net462") `
    -LicensePath (Join-Path $PSScriptRoot "LICENSE") `
    -AdditionalExcludedFilePatterns @("System.*.dll", "netstandard.dll", "Microsoft.Win32.Primitives.dll") `
    -Configuration $Configuration `
    -Force:$Force
