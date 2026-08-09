# SteamGridDB Metadata

- Identity: `SteamGridDBMetadata_f9a763e1-1ccb-4d7d-b955-d59e708f71c1`
- Installed version: `1.0`
- Status: editable Osiris source recovered and upstream provenance recorded in
  `UPSTREAM.md`. Upstream contains no explicit license file, so redistribution
  remains blocked pending permission or license clarification.

## Osiris changes

- Present the provider consistently as **SteamGridDB Metadata** while retaining
  its original extension ID, assembly name, namespace, data directory, and
  settings file for compatibility.
- Replace the Playnite-oriented artwork selectors with an Osiris-focused
  **General**, **Account**, and **Danger Zone** settings flow.
- Apply the adult-content and humorous-artwork switches globally to covers,
  heroes, logos, and icons, including Osiris' direct source picker.
- Let users apply either the same best-ranked hero/logo to similar Osiris media
  roles or different artwork. In Different mode, Game Details receives the
  best-ranked result and the horizontal library role receives the next distinct
  result, falling back to the best result when no alternative exists.
- Keep cover dimensions and endpoint choices fixed to Osiris-compatible values;
  the legacy properties remain serialized only for settings migration.

## Build

Run `build.ps1`. The build references the Development Osiris SDK and installed
RestSharp/Newtonsoft dependencies, and writes output beneath the ignored
`source/bin` directory.
