# Exophase for Osiris

Exophase is an Osiris Extras extension intended to unify trophies,
achievements, play time, and platform associations from a user's Exophase
profile with matching games in Osiris.

## Current prototype

Version 0.2.7 provides:

- Account, General, and Danger Zone settings pages.
- A first-party Osiris embedded-browser sign-in flow for Exophase.
- Automatic profile-ID discovery from the signed-in account, an Exophase
  username, a public profile URL, or a numeric player-profile ID.
- A paginated activity extractor for Exophase's current profile JSON endpoint.
- Atomic raw and normalized snapshots in the extension's private Osiris data
  directory, making the last successful import available offline.
- Per-platform activity aggregation and deterministic normalized-title matching
  against the Osiris library.
- A global Exophase page whose game table fills the available viewport like
  HowLongToBeat. The page itself does not scroll; longer libraries scroll only
  inside the table.
- An optional game-details card with a compact one-column platform
  list using the HowLongToBeat card's row geometry. Each row shows a large
  outlined platform icon, platform name, and compact hours/minutes played.
  The card remains hidden until a game has synchronized or manually added
  platform activity.
- Per-game controls in Game Edit for enabling Exophase, reviewing and
  correcting synchronized platform times, explicitly linking any number of
  differently named editions from the synchronized Exophase library, and
  adding or removing manual platforms. Linked editions are combined by
  platform; match choices and manual corrections are private extension data
  and survive a restart.
- An opt-in **Display Total Time Played** setting. It replaces the hero's
  native **Time Played** value when Exophase has positive time on a different
  platform. Osiris's native value remains authoritative for the game's current
  source: a Steam-library game uses native Time Played for Steam, an Xbox-library
  game uses it for Xbox, and a manually added game uses it for an Osiris bucket.
  That current source counts as a platform even while its native value is zero.
  Exophase contributes only the other platforms, so the current library platform
  can never be counted twice.
- Display-only totals: Exophase no longer writes its aggregate into the native
  game play-time field.
- A narrow optional runtime contract that lets HowLongToBeat compare its
  completion estimates with the same guarded Total Time Played value displayed
  by Osiris, without coupling either extension at build time.
- A read-only aggregate runtime contract for the optional Stats extension. It
  exposes guarded per-game coverage, resolved platform playtime, and 100%
  trophy completion from the same resolver used by Exophase's own page. It
  performs no synchronization or network access and keeps Stats decoupled at
  build time.
- Manual and startup synchronization with progress in Osiris's bottom panel.
- Session verification and local webview-cookie sign-out.

The extractor is intentionally marked as a prototype. Exophase has not
published a supported third-party API contract, and its current endpoint may
change without notice. The extension therefore retains the raw response,
validates its shape before matching library titles, skips ambiguous matches,
and does not replace, delete, or reduce native library data.

Credentials are entered only on Exophase's own sign-in page. The extension
does not read or store the user's password.

## Development navigation fix (unreleased)

Game titles on the global page use the same Details navigation sequence as
Home and Library, including a selection refresh after native view rebinding.
The owning window is captured before leaving the extension page so the refresh
survives page detachment. Back retains the originating extension page, and a
pending refresh cannot override a newer navigation. The SDK fallback remains
available when the Osiris theme navigation bridge is absent.

See the shared [navigation regression checks](../../validation/ExtensionGameNavigation/README.md).

The bottom summary uses extension-independent labels: **Games with Data**,
**Games without Data**, and **Games manually disabled**. The existing trophy
progress legend and library-wide counter behavior are unchanged.

Completed trophy counts/icons, the completed dashed outline and its legend use
the rarest unlocked trophy's purple border accent (`#9874C2`). Platform playtime
percentages remain neutral grey regardless of trophy completion: those values
describe the distribution of playtime, not achievement progress.

## First synchronization

1. Enable account connection and sign in to Exophase.
2. Optionally enter an Exophase username or full profile URL. Leaving it blank
   asks Osiris to discover the profile from the signed-in account.
3. Open General and choose **Synchronise now**.

Profile visibility and connected-platform privacy settings determine which
records Exophase returns. The game-details card and optional display total read
platform play time from the offline snapshot plus private per-game manual
corrections. The native Osiris play-time field remains independent and is the
source of truth for the game's current library platform.

Platform vector marks are sourced from the CC0-licensed Simple Icons project;
see `THIRD-PARTY-NOTICES.md`. Brand marks identify their respective platforms
and do not imply endorsement.

The read-only `GetUnlockedAchievementsForOsiris(Guid, CancellationToken)` bridge
supplies verified individual earned awards for local games' saved linked editions
to Trophies. It uses the extension's own website session and request fallback,
validates game/profile identities before returning, and reports partial platform
failures. Completion totals never generate individual unlocks. Steam and Xbox
integrated-library achievements remain outside this bridge.
The achievement player ID comes from the linked platform URL fragment, not the
overall profile ID used for the games library endpoint. These identities are
kept separate and revalidated before returning the result.
