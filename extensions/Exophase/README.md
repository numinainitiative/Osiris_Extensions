# Exophase for Osiris

Exophase is an Osiris Extras extension intended to unify trophies,
achievements, play time, and platform associations from a user's Exophase
profile with matching games in Osiris.

## Current prototype

Version 0.2.0 provides:

- Account, General, and Danger Zone settings pages.
- A first-party Osiris embedded-browser sign-in flow for Exophase.
- Automatic profile-ID discovery from the signed-in account, an Exophase
  username, a public profile URL, or a numeric player-profile ID.
- A paginated activity extractor for Exophase's current profile JSON endpoint.
- Atomic raw and normalized snapshots in the extension's private Osiris data
  directory, making the last successful import available offline.
- Per-platform activity aggregation and deterministic normalized-title matching
  against the Osiris library.
- An always-available game-details card with a compact one-column platform
  list using the HowLongToBeat card's row geometry. Each row shows a large
  outlined platform icon, platform name, and compact hours/minutes played.
  Before the first synchronization, or when no matching activity exists, the
  card explains what data is missing.
- Per-game controls in Game Edit for enabling Exophase, reviewing and
  correcting synchronized platform times, and adding or removing manual
  platforms. Manual corrections are private extension data and survive a
  restart.
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
- Manual and startup synchronization with progress in Osiris's bottom panel.
- Session verification and local webview-cookie sign-out.

The extractor is intentionally marked as a prototype. Exophase has not
published a supported third-party API contract, and its current endpoint may
change without notice. The extension therefore retains the raw response,
validates its shape before matching library titles, skips ambiguous matches,
and does not replace, delete, or reduce native library data.

Credentials are entered only on Exophase's own sign-in page. The extension
does not read or store the user's password.

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
