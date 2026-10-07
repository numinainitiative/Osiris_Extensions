Exophase 0.2.7

- Adds a read-only achievement bridge for Trophies 0.1.0, reusing the existing per-game saved Exophase editions and synchronised profile.
- Supplies individual verified earned awards, UTC dates and platforms across the linked editions; Trophies remains responsible for matching its catalogue and displaying results.
- Checks profile and game identities, bounds network responses and preserves existing results on partial platform failures.

Local trophy updates are explicitly requested with Game Edit -> Trophies -> Sync. Existing Exophase play-time behaviour is unchanged. Website access restrictions can prevent live trophy updates; aggregate counts are never treated as individual unlock evidence. No credentials or browser state are exposed through the bridge or included in the package.
