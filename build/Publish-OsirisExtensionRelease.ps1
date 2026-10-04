[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ExtensionName,
    [Parameter(Mandatory=$true)][string]$Version,
    [Parameter(Mandatory=$true)][string]$Tag,
    [Parameter(Mandatory=$true)][string]$Commit,
    [Parameter(Mandatory=$true)][string]$ReleaseNotesFile
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$package = Join-Path $repo "artifacts\$ExtensionName\$Version\${ExtensionName}_$Version.pext"
$checksum = "$package.sha256.json"
$metadata = Get-Content -LiteralPath $checksum -Raw | ConvertFrom-Json
if ($metadata.version -ne $Version -or $metadata.size -ne (Get-Item $package).Length -or $metadata.sha256 -ne (Get-FileHash $package -Algorithm SHA256).Hash.ToLowerInvariant()) { throw 'Extension release checksum validation failed.' }
$token = [Environment]::GetEnvironmentVariable('OSIRIS_GITHUB_TOKEN')
if ([string]::IsNullOrWhiteSpace($token)) { throw 'OSIRIS_GITHUB_TOKEN is required.' }
$headers = @{Authorization="Bearer $token";Accept='application/vnd.github+json';'User-Agent'='Osiris-Extension-Release-Publisher';'X-GitHub-Api-Version'='2022-11-28'}
$api = 'https://api.github.com/repos/numinainitiative/Osiris_Extensions'
$existing = $null
try { $existing = Invoke-RestMethod "$api/releases/tags/$Tag" -Headers $headers } catch { if (-not $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 404) { throw } }
if ($existing) { throw "Release already exists: $Tag. Refusing replacement." }
$payload = @{tag_name=$Tag;target_commitish=$Commit;name="$ExtensionName $Version";body=[IO.File]::ReadAllText([IO.Path]::GetFullPath($ReleaseNotesFile));draft=$true;prerelease=$false;make_latest='false'} | ConvertTo-Json
$release = Invoke-RestMethod -Method Post "$api/releases" -Headers $headers -ContentType 'application/json' -Body ([Text.Encoding]::UTF8.GetBytes($payload))
$upload = ([string]$release.upload_url).Split('{')[0]
foreach ($asset in @($package, $checksum)) {
    $contentType = if ($asset -eq $package) { 'application/octet-stream' } else { 'application/json' }
    $uploaded = Invoke-RestMethod -Method Post ($upload+'?name='+[Uri]::EscapeDataString([IO.Path]::GetFileName($asset))) -Headers $headers -ContentType $contentType -InFile $asset
    if ($uploaded.state -ne 'uploaded' -or $uploaded.size -ne (Get-Item $asset).Length) { throw 'Incomplete release asset upload; draft retained.' }
    if ($uploaded.digest -and $uploaded.digest -ne ('sha256:'+(Get-FileHash $asset -Algorithm SHA256).Hash.ToLowerInvariant())) { throw 'GitHub upload digest mismatch; draft retained.' }
}
$verified = Invoke-RestMethod "$api/releases/$($release.id)" -Headers $headers
if (@($verified.assets).Count -ne 2) { throw 'Missing extension release assets; draft retained.' }
$published = Invoke-RestMethod -Method Patch "$api/releases/$($release.id)" -Headers $headers -ContentType 'application/json' -Body '{"draft":false,"make_latest":"false"}'
Write-Output "Published verified release: $($published.html_url)"
