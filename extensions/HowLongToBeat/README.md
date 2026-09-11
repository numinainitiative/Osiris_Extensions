# HowLongToBeat

An original Osiris extension that displays three completion estimates on the
Game Details page:

- Main Story
- Main + Extras
- Completionist

Each estimate includes a played-time progress bar. The striped portion is time
remaining and the solid white portion compares Osiris's locally stored playtime
with that estimate, capped at a complete bar once the estimate is reached.

The extension is intentionally small. It contains one custom Game Details
control, one network client, and one persistent cache. Its General settings let
users choose the Rushed, Average, Median, or Leisure profile and independently
show Main Story, Main + Extras, and Completionist. The standard Danger Zone is
also available, while each game's Edit Game menu can enable or disable the card
and select the correct search result manually. Successfully downloaded values
remain in extension data without expiry, so they continue working offline until
the user deliberately rematches or changes that game. General also provides an
Update Database action that checks only already-fetched games against their
stored remote IDs; it never searches for a different match or populates an
unfetched game. Successful automatic and manual matches are also embedded in
the extension's per-game records, keyed by the permanent Osiris game ID. It has
no account integration, telemetry, or third-party runtime dependencies.

## Identity

- ID: `HowLongToBeat_fba3e63d-d1a1-4b9d-91c6-091a1220377d`
- Version: `1.1.2`
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
