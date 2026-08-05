param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$extensionRoot = $PSScriptRoot
$project = Join-Path $extensionRoot "source\Libraries\SteamLibrary\SteamLibrary.csproj"
$packagesConfig = Join-Path $extensionRoot "source\Libraries\SteamLibrary\packages.config"
$packagesDirectory = Join-Path $extensionRoot "source\packages"
$toolDirectory = Join-Path ([IO.Path]::GetTempPath()) "osiris-steam-library-build-tools"
$nuget = Join-Path $toolDirectory "nuget.exe"

if (!(Test-Path -LiteralPath $toolDirectory)) {
    New-Item -ItemType Directory -Path $toolDirectory -Force | Out-Null
}

if (!(Test-Path -LiteralPath $nuget)) {
    Invoke-WebRequest `
        -Uri "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" `
        -OutFile $nuget
}

& $nuget restore $packagesConfig `
    -PackagesDirectory $packagesDirectory `
    -NonInteractive
if ($LASTEXITCODE -ne 0) {
    throw "NuGet restore failed with exit code $LASTEXITCODE."
}

$vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
if (!(Test-Path -LiteralPath $vswhere)) {
    throw "Visual Studio Installer's vswhere.exe was not found."
}

$msbuild = & $vswhere `
    -latest `
    -products * `
    -requires Microsoft.Component.MSBuild `
    -find "MSBuild\**\Bin\MSBuild.exe" |
    Select-Object -First 1

if ([string]::IsNullOrWhiteSpace($msbuild)) {
    throw "A Visual Studio MSBuild installation was not found."
}

& $msbuild $project "/p:Configuration=$Configuration" /m /v:minimal
if ($LASTEXITCODE -ne 0) {
    throw "Steam Library build failed with exit code $LASTEXITCODE."
}

$output = Join-Path $extensionRoot "source\Libraries\SteamLibrary\bin\$Configuration"
Write-Host "Steam Library build complete: $output"
