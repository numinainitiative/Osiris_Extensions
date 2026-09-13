$ErrorActionPreference = "Stop"

# The Exophase mark was supplied by the user as the canonical raster artwork.
# Do not regenerate or resample it: this guard makes accidental replacement
# fail loudly during future branding work.
$target = Join-Path $PSScriptRoot "icon.png"
$expectedSha256 = "9593C1A56EE86A727ADD15D55FE61FC9FEEE56BA01C12BB6D409C8F918D7BC79"
if (-not (Test-Path -LiteralPath $target -PathType Leaf)) {
    throw "The supplied Exophase icon is missing: $target"
}

$actualSha256 = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
if ($actualSha256 -ne $expectedSha256) {
    throw "The Exophase icon does not match the supplied canonical artwork."
}

Write-Host "Verified supplied Exophase icon: $target"
