[CmdletBinding()]
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $repoRoot "extensions\GameGallery\source"
$projectPath = Join-Path $sourceRoot "GameGallery.csproj"
$solutionPath = Join-Path $sourceRoot "SteamScreenshots.sln"
$version = "2.0.1"
$extensionId = "GameGallery_8e77fe31-5e62-41e2-8fa2-64844cfd5b6b"
$artifactRoot = Join-Path $repoRoot "artifacts\GameGallery\$version"
$stageRoot = Join-Path $artifactRoot $extensionId
$packagePath = Join-Path $artifactRoot "GameGallery_$version.pext"
$checksumPath = "$packagePath.sha256.json"

$msbuildCandidates = @(
    "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
)
$msbuild = $msbuildCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $msbuild) {
    $command = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    $msbuild = if ($command) { $command.Source } else { $null }
}
if (-not $msbuild) {
    throw "Visual Studio MSBuild is required to compile Game Gallery."
}

& $msbuild $solutionPath /t:Restore /p:RestorePackagesConfig=true /nologo /verbosity:minimal
if ($LASTEXITCODE -ne 0) {
    throw "Game Gallery dependency restore failed with exit code $LASTEXITCODE."
}

& $msbuild $projectPath /t:Rebuild "/p:Configuration=$Configuration" /nologo /verbosity:minimal
if ($LASTEXITCODE -ne 0) {
    throw "Game Gallery compilation failed with exit code $LASTEXITCODE."
}

$outputRoot = Join-Path $sourceRoot "bin\$Configuration"
$manifestPath = Join-Path $outputRoot "extension.yaml"
$manifest = Get-Content -LiteralPath $manifestPath -Raw
foreach ($required in @(
    "Id: $extensionId",
    "Name: Game Gallery",
    "Version: $version",
    "Module: GameGallery.dll"
)) {
    if (-not $manifest.Contains($required)) {
        throw "The compiled extension manifest is missing '$required'."
    }
}

$controlMarkup = Get-Content -LiteralPath (Join-Path $sourceRoot "ScreenshotsControl\SteamScreenshotsControl.xaml") -Raw
foreach ($requiredName in @("OldImage", "NewImage", "ScreenshotsListBox")) {
    if (-not $controlMarkup.Contains("x:Name=`"$requiredName`"")) {
        throw "The Osiris Gallery presentation contract is missing '$requiredName'."
    }
}

$resolvedRepo = [IO.Path]::GetFullPath($repoRoot).TrimEnd('\')
$resolvedArtifacts = [IO.Path]::GetFullPath($artifactRoot).TrimEnd('\')
if (-not $resolvedArtifacts.StartsWith($resolvedRepo + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean an artifact path outside the Extensions repository."
}
if (Test-Path -LiteralPath $artifactRoot) {
    Remove-Item -LiteralPath $artifactRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $stageRoot -Force | Out-Null

$packageFiles = @(
    "extension.yaml",
    "icon.png",
    "GameGallery.dll",
    "GameGallery.dll.config",
    "Microsoft.Bcl.AsyncInterfaces.dll",
    "Microsoft.Extensions.DependencyInjection.Abstractions.dll",
    "Microsoft.Extensions.DependencyInjection.dll",
    "Microsoft.Extensions.Http.dll",
    "Microsoft.Extensions.Logging.Abstractions.dll",
    "Microsoft.Extensions.Logging.dll",
    "Microsoft.Extensions.Options.dll",
    "Microsoft.Extensions.Primitives.dll",
    "System.Diagnostics.DiagnosticSource.dll",
    "System.Threading.Tasks.Extensions.dll"
)
foreach ($relativePath in $packageFiles) {
    $sourcePath = Join-Path $outputRoot $relativePath
    if (-not (Test-Path -LiteralPath $sourcePath)) {
        throw "Required package file is missing: $relativePath"
    }
    Copy-Item -LiteralPath $sourcePath -Destination (Join-Path $stageRoot $relativePath)
}
Copy-Item -LiteralPath (Join-Path $outputRoot "Localization") -Destination $stageRoot -Recurse

$licensesRoot = Join-Path $stageRoot "Licenses"
New-Item -ItemType Directory -Path $licensesRoot -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot "extensions\GameGallery\LICENSE") `
    -Destination (Join-Path $licensesRoot "Extension-License.txt")
$dotnetLicense = Join-Path $sourceRoot "packages\Microsoft.Extensions.DependencyInjection.6.0.0\LICENSE.TXT"
if (-not (Test-Path -LiteralPath $dotnetLicense -PathType Leaf)) {
    throw "The .NET libraries license file is missing: $dotnetLicense"
}
Copy-Item -LiteralPath $dotnetLicense -Destination (Join-Path $licensesRoot "DotNet-Libraries-MIT.txt")

$forbidden = Get-ChildItem -LiteralPath $stageRoot -Recurse -File | Where-Object {
    $_.Name -match '\.(pdb|log)$' -or
    $_.Name -eq "Playnite.SDK.dll" -or
    $_.FullName -match '[\\/](Data|ExtensionsData)[\\/]'
}
if ($forbidden) {
    throw "Forbidden files entered the package: $($forbidden.FullName -join ', ')"
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory(
    $stageRoot,
    $packagePath,
    [IO.Compression.CompressionLevel]::Optimal,
    $false)

$hash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
$checksum = [ordered]@{
    schemaVersion = 1
    extensionId = $extensionId
    version = $version
    asset = [IO.Path]::GetFileName($packagePath)
    sha256 = $hash
    size = (Get-Item -LiteralPath $packagePath).Length
}
$checksum | ConvertTo-Json | Set-Content -LiteralPath $checksumPath -Encoding UTF8

[pscustomobject]@{
    Package = $packagePath
    Checksum = $checksumPath
    Sha256 = $hash
    Size = $checksum.size
    Files = (Get-ChildItem -LiteralPath $stageRoot -Recurse -File).Count
}
