# Exophase 0.2.3

This compatibility patch lets other Osiris Extras use the same guarded
cross-platform total that Exophase displays.

- Expose the effective displayed playtime through a narrow optional runtime
  contract for HowLongToBeat.
- Return Total Time Played only when the user's display override is enabled and
  the existing per-game matching and aggregation safeguards permit it.
- Fall back to authoritative native Osiris or integrated-library time in every
  other case.
- Keep the contract display-only; Exophase still never overwrites stored game
  playtime.
