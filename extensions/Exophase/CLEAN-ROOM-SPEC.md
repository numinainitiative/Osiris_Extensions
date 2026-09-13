# Exophase clean-room specification

## Purpose

Provide Osiris users with one local view of their cross-platform trophies,
achievements, play time, and platform associations as reported by Exophase.

## Implementation boundary

- This source is an original Numina Initiative implementation.
- The embedded browser shell follows the existing Osiris-maintained Steam
  Library sign-in presentation so account windows remain consistent.
- No Exophase client code, proprietary Exophase assets, credentials, or private
  API material are included.
- Platform marks used by the activity list are vendored as vector path
  data from the CC0-licensed Simple Icons project. Exact sources and trademark
  handling are recorded in `THIRD-PARTY-NOTICES.md`; the marks are used only
  to identify platform data returned by Exophase.
- Version 0.2.0 contains an original parser for the JSON delivered by the
  current `api.exophase.com/public/player/{playerProfileId}/games` profile
  endpoint. The contract is undocumented and unsupported, so the extractor is
  isolated behind validation, caching, and fail-closed matching.
- Endpoint and field behavior was independently verified from public Exophase
  pages and a separately licensed MIT utility (`TripShuti/SELFexophase`). No
  implementation code was copied. The observed interoperability fields are:
  `success`, `games`, `meta.title`, `meta.platforms`, `environment`,
  `playtimeUnits`, achievement totals, remote IDs, and last-played time.

## Data and privacy

- Passwords are entered on Exophase's own HTTPS sign-in page.
- Osiris stores no password.
- Authentication cookies remain in Osiris's webview cookie store and can be
  cleared from the Account settings page.
- Raw and normalized imports are stored only in the extension's Osiris
  user-data directory and must never be committed or packaged.
- A successful normalized snapshot is written atomically. Failed or malformed
  requests do not replace the last usable cache.
- Imported activity, per-game enable state, and manual platform corrections
  remain in the extension's private data directory.
- Exophase totals are display-only and never overwrite the native game
  play-time field. When enabled, the total appears when a platform different
  from the current library source has positive time. The current source counts
  even when native Time Played is zero. The native field is authoritative for
  that source (Osiris, Steam, or Xbox); the matching Exophase bucket is skipped
  and only other platform buckets are added, preventing duplicate counting.

## Compatibility boundary

Exophase's administrator has stated that there is no supported public API for
this use case. This prototype uses the same read-only profile data requested by
the website and does not attempt to bypass authentication, modify Exophase
data, or collect credentials. If the endpoint or browser verification changes,
the operation stops and leaves the library untouched.
