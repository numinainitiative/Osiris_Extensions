# Steam Library 1.0.1

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
- Replaces the inherited AngleSharp and Newtonsoft.Json dependencies with
  bounded framework-native parsing.

## Install

Download `SteamLibrary_1.0.1.pext`. The `.pext` file is the installable
ZIP-format Osiris extension package. For a manual replacement, fully close
Osiris and replace only the files inside the existing Steam Library extension
folder; never replace or delete the surrounding Osiris `Data` directory or the
Steam Library folder under `ExtensionsData`.

The accompanying JSON asset records the package size and SHA-256 checksum.
