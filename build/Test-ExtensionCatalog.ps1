[CmdletBinding()]
param(
    [string]$CatalogPath,
    [switch]$VerifyArtifacts,
    [switch]$RequireEnabled
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repositoryRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot)).TrimEnd('\')
if ([string]::IsNullOrWhiteSpace($CatalogPath)) {
    $CatalogPath = Join-Path $repositoryRoot "catalog\extensions.json"
}
$resolvedCatalog = [IO.Path]::GetFullPath($CatalogPath)
if (-not $resolvedCatalog.StartsWith($repositoryRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath $resolvedCatalog -PathType Leaf)) {
    throw "The catalog must be a file inside the Extensions repository."
}

$catalogBytes = [IO.File]::ReadAllBytes($resolvedCatalog)
if ($catalogBytes.Length -gt 1MB) {
    throw "The extension catalog exceeds the 1 MB client limit."
}

$catalog = [Text.Encoding]::UTF8.GetString($catalogBytes) | ConvertFrom-Json
if ($catalog.schemaVersion -ne 1) {
    throw "Unsupported catalog schema version."
}
if ($RequireEnabled -and -not $catalog.enabled) {
    throw "The catalog is disabled."
}

$entries = @($catalog.extensions)
if ($entries.Count -gt 256) {
    throw "The catalog exceeds the 256-entry client limit."
}

$idPattern = '^[A-Za-z0-9.-]+_[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$'
$hashPattern = '^[0-9a-fA-F]{64}$'
$categories = @('Libraries', 'Metadata', 'Extras', 'Utilities')
$states = @('public', 'release-candidate', 'blocked-security-review', 'blocked-license-review', 'private-development')
$ids = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$artifactsRoot = Join-Path $repositoryRoot "artifacts"
$validatedArtifacts = 0

function Test-TrustedGitHubUri([string]$Value, [string]$FieldName) {
    $uri = $null
    if (-not [Uri]::TryCreate($Value, [UriKind]::Absolute, [ref]$uri) -or
        $uri.Scheme -ne [Uri]::UriSchemeHttps -or
        ($uri.Host -ne 'github.com' -and
         $uri.Host -ne 'raw.githubusercontent.com' -and
         -not $uri.Host.EndsWith('.githubusercontent.com', [StringComparison]::OrdinalIgnoreCase))) {
        throw "$FieldName must use an approved GitHub HTTPS host."
    }
    return $uri
}

foreach ($entry in $entries) {
    foreach ($field in @('id', 'name', 'description', 'author', 'category', 'version', 'distribution', 'installFolder')) {
        if ([string]::IsNullOrWhiteSpace([string]$entry.$field)) {
            throw "A catalog entry is missing $field."
        }
    }

    if ($entry.id -notmatch $idPattern) {
        throw "Invalid extension ID: $($entry.id)"
    }
    if (-not $ids.Add([string]$entry.id)) {
        throw "Duplicate extension ID: $($entry.id)"
    }
    if ($entry.category -notin $categories) {
        throw "Invalid category for $($entry.id): $($entry.category)"
    }
    if ($entry.distribution -notin $states) {
        throw "Invalid distribution state for $($entry.id): $($entry.distribution)"
    }

    $parsedVersion = $null
    if (-not [Version]::TryParse([string]$entry.version, [ref]$parsedVersion)) {
        throw "Invalid version for $($entry.id): $($entry.version)"
    }
    $expectedFolder = "$($entry.category)\$($entry.id)"
    if ($entry.installFolder -ne $expectedFolder) {
        throw "Install folder mismatch for $($entry.id). Expected $expectedFolder."
    }

    $tags = @($entry.tags)
    if ($tags.Count -eq 0 -or $entry.category -notin $tags) {
        throw "$($entry.id) must include its category in tags."
    }
    foreach ($tag in $tags) {
        if ($tag -notin $categories) {
            throw "Invalid tag for $($entry.id): $tag"
        }
    }

    $hasPackage = -not [string]::IsNullOrWhiteSpace([string]$entry.packageUrl)
    $mustHavePackage = $entry.distribution -in @('public', 'release-candidate', 'blocked-security-review')
    if ($mustHavePackage -and -not $hasPackage) {
        throw "$($entry.id) is distributable but has no package URL."
    }

    if (-not [string]::IsNullOrWhiteSpace([string]$entry.iconUrl)) {
        Test-TrustedGitHubUri ([string]$entry.iconUrl) "iconUrl for $($entry.id)" | Out-Null
    }
    if (-not $hasPackage) {
        continue
    }

    $packageUri = Test-TrustedGitHubUri ([string]$entry.packageUrl) "packageUrl for $($entry.id)"
    if ([string]::IsNullOrWhiteSpace([string]$entry.releaseTag) -or
        $packageUri.AbsolutePath -notlike "*/releases/download/$($entry.releaseTag)/*") {
        throw "Package URL and release tag do not agree for $($entry.id)."
    }
    if ([string]$entry.sha256 -notmatch $hashPattern) {
        throw "Invalid SHA-256 for $($entry.id)."
    }
    if ([long]$entry.size -le 0 -or [long]$entry.size -gt 256MB) {
        throw "Invalid package size for $($entry.id)."
    }

    if (-not $VerifyArtifacts) {
        continue
    }

    $assetName = [IO.Path]::GetFileName($packageUri.AbsolutePath)
    $matches = @(Get-ChildItem -LiteralPath $artifactsRoot -Recurse -File -Filter $assetName)
    if ($matches.Count -ne 1) {
        throw "Expected exactly one local artifact named $assetName; found $($matches.Count)."
    }
    $package = $matches[0]
    if ($package.Length -ne [long]$entry.size) {
        throw "Catalog size does not match $assetName."
    }
    $actualHash = (Get-FileHash -LiteralPath $package.FullName -Algorithm SHA256).Hash
    if ($actualHash -ne [string]$entry.sha256) {
        throw "Catalog SHA-256 does not match $assetName."
    }

    $archive = [IO.Compression.ZipFile]::OpenRead($package.FullName)
    try {
        $manifestEntry = $archive.Entries | Where-Object { $_.FullName -eq 'extension.yaml' }
        if (@($manifestEntry).Count -ne 1) {
            throw "$assetName must contain one root extension.yaml."
        }
        if ($archive.Entries | Where-Object { $_.FullName -match '(^|/)(Data|ExtensionsData)(/|$)' }) {
            throw "$assetName contains private runtime data."
        }

        $reader = [IO.StreamReader]::new($manifestEntry.Open())
        try { $manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $manifestId = [regex]::Match($manifest, '(?m)^Id:\s*(\S+)\s*$').Groups[1].Value
        $manifestVersion = [regex]::Match($manifest, '(?m)^Version:\s*(\S+)\s*$').Groups[1].Value
        if ($manifestId -ne $entry.id -or $manifestVersion -ne $entry.version) {
            throw "$assetName manifest identity does not match the catalog."
        }
    }
    finally {
        $archive.Dispose()
    }
    $validatedArtifacts++
}

[pscustomobject]@{
    Catalog = $resolvedCatalog
    Enabled = [bool]$catalog.enabled
    Entries = $entries.Count
    Public = @($entries | Where-Object distribution -eq 'public').Count
    ReleaseCandidates = @($entries | Where-Object distribution -eq 'release-candidate').Count
    ArtifactsVerified = $validatedArtifacts
}
