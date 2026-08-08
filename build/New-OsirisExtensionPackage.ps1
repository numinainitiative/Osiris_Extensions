[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExtensionName,
    [Parameter(Mandatory = $true)]
    [string]$BuildScript,
    [Parameter(Mandatory = $true)]
    [string]$BuildOutput,
    [Parameter(Mandatory = $true)]
    [string]$LicensePath,
    [hashtable]$AdditionalLicenseFiles = @{},
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$Force
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repositoryRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot)).TrimEnd('\')
$resolvedBuildScript = [IO.Path]::GetFullPath($BuildScript)
$resolvedBuildOutput = [IO.Path]::GetFullPath($BuildOutput).TrimEnd('\')
$resolvedLicense = [IO.Path]::GetFullPath($LicensePath)
foreach ($sourcePath in @($resolvedBuildScript, $resolvedLicense)) {
    if (-not $sourcePath.StartsWith($repositoryRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Package input escaped the Extensions repository: $sourcePath"
    }
}

$resolvedAdditionalLicenses = @{}
foreach ($entry in $AdditionalLicenseFiles.GetEnumerator()) {
    $destinationName = [string]$entry.Key
    if ([string]::IsNullOrWhiteSpace($destinationName) -or
        $destinationName -ne [IO.Path]::GetFileName($destinationName)) {
        throw "Additional license destinations must be plain file names."
    }

    $sourcePath = [IO.Path]::GetFullPath([string]$entry.Value)
    if (-not $sourcePath.StartsWith($repositoryRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "Additional license input is missing or escaped the Extensions repository: $sourcePath"
    }
    $resolvedAdditionalLicenses[$destinationName] = $sourcePath
}

& $resolvedBuildScript -Configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "$ExtensionName build failed with exit code $LASTEXITCODE."
}

$manifestPath = Join-Path $resolvedBuildOutput "extension.yaml"
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "$ExtensionName build output is missing extension.yaml."
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw
$idMatch = [regex]::Match($manifest, '(?m)^Id:\s*(\S+)\s*$')
$versionMatch = [regex]::Match($manifest, '(?m)^Version:\s*(\S+)\s*$')
$moduleMatch = [regex]::Match($manifest, '(?m)^Module:\s*(\S+)\s*$')
if (-not $idMatch.Success -or -not $versionMatch.Success -or -not $moduleMatch.Success) {
    throw "$ExtensionName extension.yaml is missing Id, Version, or Module."
}

$extensionId = $idMatch.Groups[1].Value
$version = $versionMatch.Groups[1].Value
$module = $moduleMatch.Groups[1].Value
if (-not (Test-Path -LiteralPath (Join-Path $resolvedBuildOutput $module) -PathType Leaf)) {
    throw "$ExtensionName build output is missing its declared module."
}

$artifactRoot = Join-Path $repositoryRoot "artifacts\$ExtensionName\$version"
$stagingRoot = Join-Path $artifactRoot "staging"
$packagePath = Join-Path $artifactRoot "${ExtensionName}_$version.pext"
$checksumPath = "$packagePath.sha256.json"
$resolvedArtifacts = [IO.Path]::GetFullPath($artifactRoot).TrimEnd('\')
if (-not $resolvedArtifacts.StartsWith($repositoryRoot + '\artifacts\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing an artifact path outside the repository artifact root."
}

if (Test-Path -LiteralPath $artifactRoot) {
    if (-not $Force) {
        throw "Package output already exists: $artifactRoot. Use -Force to rebuild it."
    }
    Remove-Item -LiteralPath $artifactRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null

$excludedNames = @("Playnite.SDK.dll", "AngleSharp.dll")
foreach ($file in Get-ChildItem -LiteralPath $resolvedBuildOutput -Recurse -File) {
    if ($file.Name -in $excludedNames -or
        $file.Extension -in @('.pdb', '.xml', '.log', '.user', '.suo')) {
        continue
    }

    $relative = $file.FullName.Substring($resolvedBuildOutput.Length).TrimStart('\')
    $segments = $relative -split '[\\/]'
    if ($segments -contains "Data" -or $segments -contains "ExtensionsData") {
        throw "Private runtime data entered the package: $relative"
    }

    $destination = Join-Path $stagingRoot $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination
}

$licenses = Join-Path $stagingRoot "Licenses"
New-Item -ItemType Directory -Path $licenses -Force | Out-Null
Copy-Item -LiteralPath $resolvedLicense -Destination (Join-Path $licenses "Extension-License.txt")
foreach ($entry in $resolvedAdditionalLicenses.GetEnumerator()) {
    Copy-Item -LiteralPath $entry.Value -Destination (Join-Path $licenses $entry.Key)
}

$stagedManifest = Join-Path $stagingRoot "extension.yaml"
$stagedModule = Join-Path $stagingRoot $module
if (-not (Test-Path -LiteralPath $stagedManifest -PathType Leaf) -or
    -not (Test-Path -LiteralPath $stagedModule -PathType Leaf)) {
    throw "The staged package lost its manifest or module."
}

[IO.Compression.ZipFile]::CreateFromDirectory(
    $stagingRoot,
    $packagePath,
    [IO.Compression.CompressionLevel]::Optimal,
    $false)

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

[pscustomobject]@{
    Extension = $ExtensionName
    Package = $packagePath
    Checksum = $checksumPath
    Sha256 = $hash
    Size = $package.Length
    Files = @(Get-ChildItem -LiteralPath $stagingRoot -Recurse -File).Count
}
