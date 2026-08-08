# Osiris extension catalog

`extensions.json` is the signed-by-Git-history discovery document used by the
Osiris Browse page. The client accepts schema version 1, downloads only entries
whose `distribution` is `public`, and rejects packages that do not match the
declared identity, version, byte size, and SHA-256.

The global `enabled` switch is a release circuit breaker. Keep it `false` while
the repository is private, while public release assets are missing, or while a
catalog migration is incomplete. Non-public states are retained for release
engineering but never appear in Browse:

- `release-candidate`: package and license records are ready for final review.
- `blocked-security-review`: package exists but inherited dependencies must be
  remediated and regression-tested.
- `blocked-license-review`: redistribution rights are not yet established.
- `private-development`: incomplete internal work.

Release packages use immutable GitHub Release URLs. Catalog icons are small
repository files served from `raw.githubusercontent.com`; Osiris caches each
icon locally with a two-megabyte download limit.

Validate before every catalog change:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File `
  .\build\Test-ExtensionCatalog.ps1 -VerifyArtifacts
```
