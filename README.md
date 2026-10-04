# Osiris Extensions

This repository is the authoritative source and public catalog for extensions
offered through Osiris.

Each extension has its own folder, identity, version, source history, license
record, build process, and release package. Osiris reads
`catalog/extensions.json`, renders entries marked `public`, downloads their
packages from this repository's GitHub Releases, validates the declared byte
size and SHA-256, and queues restart-safe installation or update.

The catalog is enabled and public extension packages are distributed only as
GitHub Release assets. Installed binaries recovered from a user installation
are preserved outside Git and are not treated as editable source. Compiled
`.pext` files remain ignored and do not belong in normal Git history.

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
5. Confirm the repository, catalog, and every public release URL are anonymously
   accessible.

Published versions are monotonic. Current releases include Game Gallery
`2.0.4`, Steam Library `1.0.3`, Stats `0.1.3`, and Screenshots Gallery `0.1.0`;
resetting existing extension
versions would break update ordering for installed copies.

Stats insights and Screenshots Gallery's game-details/editor surfaces require
Osiris Beta 0.0.50 or newer. Update the application before these extensions.
`build/Publish-OsirisExtensionRelease.ps1` creates a draft, uploads the package
and checksum, verifies GitHub asset sizes and digests, then publishes. Promote
the matching catalog entry only after upload verification.
