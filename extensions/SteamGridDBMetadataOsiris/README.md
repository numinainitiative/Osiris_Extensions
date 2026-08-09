# SteamGridDB Metadata for Osiris

An original Osiris metadata provider that downloads community artwork through
the public SteamGridDB API.

## Development status

Version `0.1.0` is an unreleased development build. It is intentionally kept
out of the public Osiris extension catalog until local integration and clean
installation testing are complete.

## Features

- Vertical covers, heroes, icons, logos, and horizontal-library artwork.
- Title search and direct Steam application ID lookup.
- Global adult-content and humorous-artwork filters.
- Same or different ranked artwork for related Osiris media roles.
- User-supplied SteamGridDB API key; no key is bundled.

See [CLEAN-ROOM-SPEC.md](CLEAN-ROOM-SPEC.md) for the implementation boundary
and behavioral contract.

## License

MIT. SteamGridDB is a third-party service and is not affiliated with Osiris or
Numina Initiative.
