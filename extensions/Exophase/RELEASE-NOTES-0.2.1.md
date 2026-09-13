# Exophase 0.2.1

This patch makes Exophase matching and Game Details presentation more useful
when platform stores use a different title from the game in Osiris.

- Add an explicit per-game Exophase search and match control in Game Edit.
  Searches use the last synchronized offline snapshot and tolerate common
  edition, remaster, and subtitle differences.
- Preview the selected Exophase platform activity immediately while retaining
  the game's manually entered platform rows.
- Persist explicit matches in the extension's private per-game settings so
  future synchronization keeps the chosen association.
- Hide the Exophase Game Details card when a game has no synchronized or
  manually added platform activity; the local Osiris baseline alone no longer
  creates an empty card.
- Use the canonical rounded Exophase artwork on the public Browse card.

Exophase does not publish a supported API contract for this profile data. The
extension continues to validate responses, retain the last usable snapshot on
failure, and keep Osiris's native Time Played value authoritative.
