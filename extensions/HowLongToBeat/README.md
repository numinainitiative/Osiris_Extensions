# HowLongToBeat

An original Osiris extension that displays three completion estimates on the
Game Details page:

- Main Story
- Main + Extras
- Completionist

Each estimate includes a played-time progress bar. The striped portion is time
remaining and the solid white portion compares Osiris's effective displayed
playtime with that estimate, capped at a complete bar once the estimate is
reached. When Exophase's optional **Display Total Time Played** setting is
active for a game, this includes its safe cross-platform total; otherwise it
uses the native Osiris or integrated-library time.

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

The extension also exposes a read-only in-process bridge for the Stats global
page. It returns the currently selected completion-time profile and each
game's already saved manual, automatic, or cached estimates. Reading that
bridge never searches HowLongToBeat or downloads new data.

## Development navigation fix (unreleased)

Game titles on the global page use the same Details navigation sequence as
Home and Library, including a selection refresh after native view rebinding.
The owning window is captured before leaving the extension page so the refresh
survives page detachment. Back retains the originating extension page, and a
pending refresh cannot override a newer navigation. The SDK fallback remains
available when the Osiris theme navigation bridge is absent.

See the shared [navigation regression checks](../../validation/ExtensionGameNavigation/README.md).

## Page footer (1.2.5)

The global page has an Exophase-style fixed bottom legend: solid white for time
played, vertical stripes for Main Story, small diagonal stripes for Main + Extras,
and large diagonal stripes for Completionist. All four samples reuse the actual
timeline renderer. Library-wide
counts show games with usable completion data, without data for the selected
time profile, and manually disabled games. Counts refresh with the page's stored
data and are independent of sorting, search, and the played-games filter. The
labels are **Games with Data**, **Games without Data**, and **Games manually disabled**.
Both the sample and counter groups wrap on narrower windows; only the table scrolls. This display
does not fetch data or change game settings.

## Identity

- ID: `HowLongToBeat_fba3e63d-d1a1-4b9d-91c6-091a1220377d`
- Version: `1.2.5`
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
