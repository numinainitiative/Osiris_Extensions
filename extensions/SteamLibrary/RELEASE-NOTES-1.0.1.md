# Steam Library 1.0.1 — Private development prerelease

This private prerelease is intended for manual Osiris acceptance testing. It is
not approved for public distribution yet.

## Changes

- Fixes cropped vertical covers by resolving Steam's declared 2:3 library
  capsule assets.
- Adds the redesigned Osiris authentication middle window while preserving the
  existing Steam login and token flow.
- Removes the obsolete tag-blacklist option and its hidden import filtering.
- Keeps the metadata tag-count field directly editable.
- Aligns General, Additional Accounts, Metadata, and Advanced settings markup
  with the Osiris extension-settings host.
- Preserves the existing Steam Library extension identity so imported games and
  extension settings continue to use the same profile locations.

## Install

Download `SteamLibrary_1.0.1.pext`. The `.pext` file is the installable
ZIP-format Osiris extension package. For a manual replacement, fully close
Osiris and replace only the files inside the existing Steam Library extension
folder; never replace or delete the surrounding Osiris `Data` directory or the
Steam Library folder under `ExtensionsData`.

## Private-release warning

The inherited Steam Library 2.40 baseline uses AngleSharp 0.9.9 and
Newtonsoft.Json 10.0.3, which currently have known NuGet security advisories.
Keep this release private until those dependencies are upgraded and Steam
authentication, account import, metadata, installation, and game launch receive
complete regression coverage.

The accompanying JSON asset records the package size and SHA-256 checksum.
