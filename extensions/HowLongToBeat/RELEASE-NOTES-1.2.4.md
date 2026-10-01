HowLongToBeat 1.2.4

- Restores Edit Game search after HowLongToBeat changed its initialization response to a token-only handshake.
- Keeps compatibility with the older proof-key handshake when those fields are supplied.
- Retries once with a fresh search token when the search request is rejected with HTTP 403.
