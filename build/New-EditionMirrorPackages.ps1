[CmdletBinding()]
param([string]$OsirisRepository = (Join-Path $PSScriptRoot '..\..\Osiris_Launcher'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$appRepo = [IO.Path]::GetFullPath($OsirisRepository)
$bridgeProject = Join-Path $repo 'shared\EditionMirrorBridge\EditionMirrorBridge.csproj'
$adapterProject = Join-Path $appRepo 'tools\EditionMirrorExtensions\EditionMirrorExtensions.csproj'
& dotnet build $bridgeProject -c Release --verbosity minimal
if ($LASTEXITCODE) { throw 'Bridge build failed.' }
& dotnet build $adapterProject -c Release --verbosity minimal
if ($LASTEXITCODE) { throw 'Adapter build failed.' }
$bridge = Join-Path $repo 'shared\EditionMirrorBridge\bin\Release\net462\Osiris.EditionMirrorBridge.dll'
$adapter = Join-Path $appRepo 'tools\EditionMirrorExtensions\bin\Release\net8.0\EditionMirrorExtensions.dll'
$specs = Get-Content (Join-Path $repo 'shared\EditionMirrorBridge\releases.json') -Raw | ConvertFrom-Json
foreach ($spec in $specs) {
    $baseline = Join-Path $repo "artifacts\$($spec.name)\$($spec.baselineVersion)\$($spec.name)_$($spec.baselineVersion).pext"
    if (!(Test-Path -LiteralPath $baseline)) {
        New-Item -ItemType Directory -Path (Split-Path $baseline) -Force | Out-Null
        Invoke-WebRequest $spec.baselineUrl -OutFile $baseline
    }
    if ((Get-FileHash -LiteralPath $baseline).Hash.ToLowerInvariant() -ne $spec.baselineSha256) { throw "Baseline checksum mismatch: $($spec.name)" }
    $output = Join-Path $repo "artifacts\$($spec.name)\$($spec.version)"
    if (Test-Path -LiteralPath $output) { throw "Output exists; preserve immutable candidate: $output" }
    $stage = Join-Path $output 'staging'
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    [IO.Compression.ZipFile]::ExtractToDirectory($baseline, $stage)
    $manifest = Join-Path $stage 'extension.yaml'
    $yaml = Get-Content -LiteralPath $manifest -Raw
    if ($yaml -notmatch ('(?m)^Version:\s*'+[regex]::Escape($spec.baselineVersion)+'\s*$')) { throw 'Baseline manifest version mismatch.' }
    $module = [regex]::Match($yaml, '(?m)^Module:\s*(\S+)').Groups[1].Value
    $id = [regex]::Match($yaml, '(?m)^Id:\s*(\S+)').Groups[1].Value
    & dotnet $adapter $bridge (Join-Path $stage $module) $spec.page
    if ($LASTEXITCODE) { throw 'Scoped adapter failed.' }
    & dotnet $adapter $bridge (Join-Path $stage $module) $spec.page --verify
    if ($LASTEXITCODE) { throw 'Scoped adapter verification failed.' }
    Copy-Item -LiteralPath $bridge -Destination $stage
    $yaml = [regex]::Replace($yaml, '(?m)^Version:.*$', ('Version: '+$spec.version))
    [IO.File]::WriteAllText($manifest, $yaml)
    $licenses = Join-Path $stage 'Licenses'
    New-Item -ItemType Directory -Path $licenses -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $repo 'shared\EditionMirrorBridge\LICENSE') -Destination (Join-Path $licenses 'EditionMirrorBridge-License.txt')
    Copy-Item -LiteralPath (Join-Path $repo 'shared\EditionMirrorBridge\README.md') -Destination (Join-Path $stage 'EditionMirrorBridge-NOTICE.md')
    foreach ($file in Get-ChildItem -LiteralPath $stage -Recurse -File) {
        $relative = $file.FullName.Substring($stage.Length).TrimStart('\')
        if ($relative -match '(^|[\\/])(Data|ExtensionsData|library|logs|Recovery|browsercache)([\\/]|$)' -or $file.Extension -in @('.pdb','.log','.pext')) { throw "Private/debug input: $relative" }
    }
    $package = Join-Path $output "$($spec.name)_$($spec.version).pext"
    [IO.Compression.ZipFile]::CreateFromDirectory($stage,$package,[IO.Compression.CompressionLevel]::Optimal,$false)
    $metadata = [ordered]@{schemaVersion=1;extensionId=$id;version=$spec.version;asset=[IO.Path]::GetFileName($package);sha256=(Get-FileHash -LiteralPath $package).Hash.ToLowerInvariant();size=(Get-Item $package).Length}
    $metadata | ConvertTo-Json | Set-Content -LiteralPath "$package.sha256.json" -Encoding utf8
    [pscustomobject]$metadata
}
