# HowLongToBeat

An original Osiris extension that displays three completion estimates on the
Game Details page:

- Main Story
- Main + Extras
- Completionist

The extension is intentionally small. It contains one custom Game Details
control, one network client, and one persistent cache. Its global settings page
contains the standard Danger Zone, while each game's Edit Game menu can enable
or disable the card and select the correct search result manually. It has no
account integration, telemetry, or third-party runtime dependencies.

## Identity

- ID: `HowLongToBeat_fba3e63d-d1a1-4b9d-91c6-091a1220377d`
- Version: `1.0.0`
- Author: `Osiris`
- Type: `GenericPlugin`
- Category: `Extras`

## Build

```powershell
.\build.ps1
```

The project targets .NET Framework 4.6.2 and resolves the Playnite SDK from the
Osiris Development installation by default.

## Package

```powershell
.\package.ps1
```

This uses the repository's privacy-safe common package builder. Packages are
release artifacts and are not committed to Git.
