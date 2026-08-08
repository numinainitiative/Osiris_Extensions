# Steam Metadata

- Identity: `UniversalSteamMetadata_f2db8fb1-4981-4dc4-b087-05c782215b72`
- Installed version: `1.0`
- Status: editable Osiris source recovered; upstream provenance and MIT license
  recorded in `UPSTREAM.md` and `LICENSE`. The separately loaded SteamKit2
  dependency retains its LGPL 2.1-or-later notice and license in `LICENSES/`.

## Osiris changes

- Use the concise user-facing name **Steam Metadata** throughout Osiris while
  retaining the original extension ID, assembly name, namespace, settings file,
  and data directory for compatibility.
- Present the native metadata, artwork, tag, and Steam Deck settings through the
  shared Osiris extension settings host.

## Build

Run `build.ps1`. The build references the Development Osiris SDK, the installed
AngleSharp runtime, and the extension's SteamKit2 dependency, and writes output
beneath the ignored `source/bin` directory.

Run `package.ps1` to rebuild and create `SteamMetadata_1.0.pext` plus its
SHA-256 manifest beneath the ignored `artifacts/SteamMetadata/1.0` directory.
The package includes the extension MIT license, the SteamKit2 notice, and the
complete LGPL 2.1 terms.
