# IGN Metadata 1.0.1

Restores IGN search results in Osiris metadata matching.

## Fixed

- Adds the required Apollo operation-name header to IGN GraphQL requests so the
  current IGN endpoint returns game search data instead of an empty result flow.
