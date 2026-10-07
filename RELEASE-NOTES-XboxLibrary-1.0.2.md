Xbox Library 1.0.2

- Adds a read-only, authenticated achievement bridge for Trophies 0.1.0.
- Resolves the integrated game's Xbox title ID and returns title-scoped awards and unlock dates without exposing authentication tokens.
- Adds cancellation, bounded pagination/response sizes and identity checks. No achievements are written to Xbox.

Existing account authentication, import, launch and installation behaviour is retained. Achievement access requires live-account acceptance testing; fixture checks do not guarantee every account or title exposes matching awards. Requires Trophies 0.1.0 and Osiris Beta 0.0.51 for the trophy presentation; normal library use does not require Trophies.
