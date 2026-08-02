# Osiris Extensions

This private repository is the future source and catalog for extensions offered
through Osiris.

Each extension has its own folder, identity, version, source history, license
record, build process, and release package. Osiris will eventually read the
catalog in `catalog/extensions.json`, download approved packages from this
repository's GitHub Releases, and notify users when an installed extension has a
newer version.

The catalog is intentionally disabled while extension ownership, source, and
redistribution licenses are audited. Installed binaries recovered from a user
installation are preserved outside Git and are not treated as editable source.

## Repository layout

- `extensions/`: one working area per extension.
- `catalog/`: machine-readable extension catalog used by Osiris.
- `docs/`: recovery, provenance, licensing, and release notes.

HowLongToBeat is third-party software and remains linked to its original author.
It will not be republished by Numina Initiative without an explicit license and
redistribution review.
