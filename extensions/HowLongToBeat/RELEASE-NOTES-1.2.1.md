# HowLongToBeat 1.2.1

This patch makes completion progress consistent with Osiris's optional unified
cross-platform playtime.

- Use Exophase's effective Total Time Played for completion progress when the
  user has enabled that display option and the game has an applicable total.
- Apply the same effective time to the Game Details card and the full HLTB
  library page, including played-time text, sorting, and the played-games
  filter.
- Keep native Osiris or integrated-library playtime whenever Exophase is not
  installed, is disabled, does not expose a safe total, or has its display
  option turned off.
- Preserve optional-extension isolation: HLTB has no compile-time dependency on
  Exophase and never changes the game's stored playtime.
