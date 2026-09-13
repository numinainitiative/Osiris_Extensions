# Exophase 0.2.0

This first public Exophase release brings cross-platform play-time activity
into Osiris while keeping the native library time authoritative.

- Connect an Exophase account through Osiris's embedded sign-in flow and keep
  the last successful activity snapshot available offline.
- Match Exophase activity to Osiris games conservatively, skipping ambiguous
  titles instead of modifying the wrong game.
- Review platform play time in a compact game-details card with accurate
  platform marks, source labels, and the standard collapsed `view more`
  behavior.
- Correct synchronized values or add manual platforms from Game Edit, with
  clearly distinguished Local, Exophase, and Manual sources.
- Optionally display Total Time Played by adding only time from platforms that
  are not the game's current Osiris or integrated-library source, preventing
  the same platform from being counted twice.
- Run synchronization manually or when Osiris starts, with progress reported
  through the standard bottom panel.

Exophase does not publish a supported API contract for this profile data. The
extension therefore validates responses, retains the last usable snapshot on
failure, and never overwrites Osiris's native Time Played value.
