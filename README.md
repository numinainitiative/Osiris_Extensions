# Osiris Extensions

This private repository is the authoritative source and catalog for extensions
offered through Osiris.

Each extension has its own folder, identity, version, source history, license
record, build process, and release package. Osiris reads
`catalog/extensions.json`, renders entries marked `public`, downloads their
packages from this repository's GitHub Releases, validates the declared byte
size and SHA-256, and queues restart-safe installation or update.

The catalog remains intentionally disabled while repository visibility and the
remaining release blockers are resolved. Installed binaries recovered from a
user installation are preserved outside Git and are not treated as editable
source. Compiled `.pext` files remain ignored and belong only in GitHub Release
assets.

## Repository layout

- `extensions/`: one working area per extension.
- `catalog/`: machine-readable extension catalog used by Osiris.
- `build/New-OsirisExtensionPackage.ps1`: common privacy-safe package builder.
- `build/Test-ExtensionCatalog.ps1`: catalog, checksum, and package validator.
- `docs/`: recovery, provenance, licensing, and release notes.

HowLongToBeat is maintained as an original, clean-room Osiris extension.

## Release gate

Before publishing an extension:

1. Build its versioned `.pext` and SHA-256 JSON manifest.
2. Run `build/Test-ExtensionCatalog.ps1 -VerifyArtifacts`.
3. Upload the `.pext` as the exact GitHub Release asset named by the catalog.
4. Change only that entry's `distribution` to `public` after its license,
   dependency, and regression checks pass.
5. Enable the global catalog only after the repository and every public release
   URL are anonymously accessible.

Published versions are monotonic. Game Gallery remains `2.0.2` and Steam
Library remains `1.0.1`; resetting them to `1.0` would break update ordering for
existing installations.
