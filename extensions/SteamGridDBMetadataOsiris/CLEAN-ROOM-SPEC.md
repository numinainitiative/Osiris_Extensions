# SteamGridDB Metadata for Osiris: clean-room specification

## Boundary

This is a new Osiris extension. It is not a fork of
`cooperate/SteamGridDBMetadata`, and its recovered source, compiled binaries,
assets, type names, implementation structure, and extension identity are not
inputs to this project.

The implementation may use only:

- the public Osiris/Playnite SDK contracts shipped with the Development Osiris
  installation;
- SteamGridDB's public API v2 contract and user-generated API keys;
- independently licensed dependencies recorded by the project; and
- the user-approved behavior in this specification.

## Identity

- Name: `SteamGridDB Metadata`
- ID: `SteamGridDBMetadata_8d89f55b-826c-43dc-8946-4038a6e182a2`
- Assembly: `Osiris.SteamGridDBMetadata.dll`
- Namespace root: `Osiris.Extensions.SteamGridDBMetadata`
- Type: `MetadataProvider`
- Initial development version: `0.1.0`
- Intended release license: MIT

## Supported media

- Vertical and horizontal cover artwork through `MetadataField.CoverImage`.
- Hero artwork through `MetadataField.BackgroundImage`.
- Logo artwork through Osiris's explicit logo media role.
- Icon artwork through `MetadataField.Icon`.
- Search by title and by supported external identifiers when available.
- Manual source selection from Game Edit -> Media.
- Automatic metadata/media downloads through Osiris's metadata-provider
  contract.

## Settings

The extension exposes `General`, `Account`, and the shared Osiris `Danger Zone`
in that order.

General:

- `Allow adult content artwork` switch, disabled by default.
- `Allow humorous artwork` switch, disabled by default.
- `Choose how media should be applied for similar kinds` dropdown with
  `Different media` as the default and `Same media` as the alternative.

Account:

- A private SteamGridDB API-key field.
- A canonical action opening the SteamGridDB preferences page.
- Validation that explains when the key is missing without sending it anywhere
  except SteamGridDB's HTTPS API.

The settings view uses the Osiris Settings -> Backup geometry and canonical
styles. Settings use the SDK `ISettings` edit transaction so Cancel restores the
pre-edit values.

## Selection rules

- Adult and humorous filters apply uniformly to every supported media role,
  manual source picker, and automatic download.
- Results preserve SteamGridDB ranking order.
- `Same media` reuses the highest-ranked applicable hero/logo for equivalent
  Osiris roles.
- `Different media` assigns the highest-ranked result to the primary game
  details role and the next distinct result to the equivalent horizontal
  library role, falling back to the first result when no alternative exists.
- The provider never uploads, votes on, deletes, or modifies SteamGridDB data.

## Privacy and distribution

- The API key is stored only under the extension's private Osiris
  `Data/ExtensionsData/Metadata` directory through the SDK settings contract.
- Logs must never contain the API key or authorization header.
- Release packages contain no downloaded artwork, user settings, cached API
  responses, browser state, logs, or Osiris profile data.
- Every packaged dependency must have a recorded redistribution license.
