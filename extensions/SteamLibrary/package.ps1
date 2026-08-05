[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$Force
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem

$extensionRoot = [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $extensionRoot "..\..")).TrimEnd('\')
$buildOutput = Join-Path $extensionRoot "source\Libraries\SteamLibrary\bin\$Configuration"
$extensionManifest = Join-Path $extensionRoot "source\Libraries\SteamLibrary\extension.yaml"

& (Join-Path $extensionRoot "build.ps1") -Configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Steam Library build failed with exit code $LASTEXITCODE."
}

$manifestText = Get-Content -LiteralPath $extensionManifest -Raw
$idMatch = [regex]::Match($manifestText, '(?m)^Id:\s*(\S+)\s*$')
$versionMatch = [regex]::Match($manifestText, '(?m)^Version:\s*(\S+)\s*$')
if (-not $idMatch.Success -or -not $versionMatch.Success) {
    throw "Steam Library extension.yaml is missing Id or Version."
}

$extensionId = $idMatch.Groups[1].Value
$version = $versionMatch.Groups[1].Value
$artifactRoot = Join-Path $repositoryRoot "artifacts\SteamLibrary\$version"
$stagingRoot = Join-Path $artifactRoot "staging"
$packagePath = Join-Path $artifactRoot "SteamLibrary_$version.pext"
$checksumPath = "$packagePath.sha256.json"

foreach ($path in @($stagingRoot, $packagePath, $checksumPath)) {
    $fullPath = [IO.Path]::GetFullPath($path)
    if (-not $fullPath.StartsWith($artifactRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Unsafe package path: $fullPath"
    }
    if (Test-Path -LiteralPath $fullPath) {
        if (-not $Force) {
            throw "Package output already exists: $fullPath. Use -Force to rebuild it."
        }
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }
}

New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null

foreach ($file in @(
    "extension.yaml",
    "plugin.cfg",
    "SteamKit2.dll",
    "SteamLibrary.dll",
    "SteamLibrary.dll.config"
)) {
    $source = Join-Path $buildOutput $file
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "Required runtime file is missing: $source"
    }
    Copy-Item -LiteralPath $source -Destination (Join-Path $stagingRoot $file)
}

foreach ($directory in @("Localization", "Resources", "SteamShared")) {
    $source = Join-Path $buildOutput $directory
    if (-not (Test-Path -LiteralPath $source -PathType Container)) {
        throw "Required runtime directory is missing: $source"
    }
    Copy-Item -LiteralPath $source -Destination (Join-Path $stagingRoot $directory) -Recurse
}

$forbidden = @(Get-ChildItem -LiteralPath $stagingRoot -Recurse -File | Where-Object {
    $_.Extension -in @('.pdb', '.xml', '.log', '.cache') -or
    $_.FullName -match '(?i)(Data|ExtensionsData|browser|credential|cookie|token|settings)'
})
if ($forbidden.Count -ne 0) {
    throw "Forbidden files entered the package: $($forbidden.FullName -join ', ')"
}

$stagedManifest = Get-Content -LiteralPath (Join-Path $stagingRoot "extension.yaml") -Raw
if ($stagedManifest -notmatch ('(?m)^Id:\s*' + [regex]::Escape($extensionId) + '\s*$') -or
    $stagedManifest -notmatch ('(?m)^Version:\s*' + [regex]::Escape($version) + '\s*$')) {
    throw "The staged extension manifest does not match the package identity."
}

[IO.Compression.ZipFile]::CreateFromDirectory(
    $stagingRoot,
    $packagePath,
    [IO.Compression.CompressionLevel]::Optimal,
    $false
)

$package = Get-Item -LiteralPath $packagePath
$hash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
[ordered]@{
    schemaVersion = 1
    extensionId = $extensionId
    version = $version
    asset = $package.Name
    sha256 = $hash
    size = $package.Length
} | ConvertTo-Json | Set-Content -LiteralPath $checksumPath -Encoding utf8

Write-Output "Steam Library package: $packagePath"
Write-Output "Checksum manifest: $checksumPath"
Write-Output "SHA-256: $hash"
