# Clean-room specification

This extension is an original implementation created for Osiris. No program
files or source code from the retired third-party package are used by this
project.

## Functional contract

1. Register one Game Details custom element named
   `CompletionTimesViewControl` under source `HowLongToBeat`.
2. When the selected game changes, debounce the request and cancel obsolete
   work.
3. Persist successful matches without expiry so fetched estimates remain
   available offline until the user deliberately rematches or changes that
   game. Retain unsuccessful searches for 24 hours before retrying.
4. Query the current public site-search flow using only the selected game's
   title and optional release year.
5. Rank results deterministically, strongly preferring an exact normalized
   title and ordinary game entries over similarly named add-ons.
6. Present Main Story, Main + Extras, and Completionist estimates in a compact
   card supplied by the Osiris theme. For each visible estimate, compare the
   game's locally stored playtime with the selected estimate and render a
   clamped zero-to-100-percent progress bar without changing the stored match.
7. Never store transient request tokens, cookies, or browser state. Do not log
   them.
8. Never refresh a successful cached match merely because time has passed.
   Manual rematching may replace it; a failed deliberate refresh must not
   remove the previously downloaded values.
9. Offer an explicit Update Database action that enumerates the user's library
   but requests details only for games with a successful stored match. Refresh
   each exact stored remote game identifier, preserve manual selections and all
   display preferences, skip unfetched games, and retain prior values whenever
   an individual request or local write fails.

The cache and per-game extension-record formats keep the remote game identifier
and the raw values in seconds. Successful automatic and manual matches are
associated with the permanent Osiris game identifier so a future
extension-owned library page can sort and aggregate the same data without
changing Osiris core.
