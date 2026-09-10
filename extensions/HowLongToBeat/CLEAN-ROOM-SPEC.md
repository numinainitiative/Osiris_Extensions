# Clean-room specification

This extension is an original implementation created for Osiris. No program
files or source code from the retired third-party package are used by this
project.

## Functional contract

1. Register one Game Details custom element named
   `CompletionTimesViewControl` under source `HowLongToBeat`.
2. When the selected game changes, debounce the request and cancel obsolete
   work.
3. Prefer fresh cached results. Retain successful matches for 30 days and
   unsuccessful searches for 24 hours.
4. Query the current public site-search flow using only the selected game's
   title and optional release year.
5. Rank results deterministically, strongly preferring an exact normalized
   title and ordinary game entries over similarly named add-ons.
6. Present Main Story, Main + Extras, and Completionist estimates in a compact
   card supplied by the Osiris theme.
7. Never store transient request tokens, cookies, or browser state. Do not log
   them.
8. If a refresh fails and an older successful cache entry exists, show the
   stale result instead of removing useful data.

The cache format keeps the remote game identifier and the raw values in
seconds so a future extension-owned library page can sort and aggregate the
same data without changing Osiris core.
