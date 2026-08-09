# SteamGridDB Metadata for Osiris

An original Osiris metadata provider that downloads community artwork through
the public SteamGridDB API.

## Release

Version `1.0` is the first public release for Osiris. It is distributed through
the official Osiris Extensions catalog and contains no API key, settings,
cached artwork, or profile data.

## Features

- Vertical covers, heroes, icons, logos, and horizontal-library artwork.
- Title search and direct Steam application ID lookup.
- Global adult-content and humorous-artwork filters.
- Same or different ranked artwork for related Osiris media roles.
- User-supplied SteamGridDB API key; no key is bundled.
- Extension-owned runtime integration for Osiris media browsing and automatic
  media selection; disabling or uninstalling the extension removes the source.

See [CLEAN-ROOM-SPEC.md](CLEAN-ROOM-SPEC.md) for the implementation boundary
and behavioral contract.

## License

MIT. SteamGridDB is a third-party service and is not affiliated with Osiris or
Numina Initiative.
