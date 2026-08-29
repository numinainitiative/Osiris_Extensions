# IGN Metadata

- Identity: `IgnMetadata_6024e3a9-de7e-4848-9101-7a2f818e7e47`
- Version: `1.0.1`
- Upstream baseline: IGN Metadata 0.6 by Jeshibu
- License: MIT

IGN Metadata downloads game details, links, ratings, artwork, and optional
HowLongToBeat fallback data from IGN. Osiris keeps the original extension ID so
existing installations and metadata matches remain compatible.

## Osiris changes

- Uses the concise user-facing name **IGN Metadata**.
- Uses the Osiris `1.0.1` release version while retaining the upstream assembly,
  namespace, extension ID, and metadata behavior.
- Presents management and removal through the shared Osiris extension settings
  host. Metadata-field selection and provider priority remain global under
  **Settings -> Metadata**.

## Build and package

Run `build.ps1` to compile the extension. Run `package.ps1` to create
`IgnMetadata_1.0.1.pext` and its SHA-256 manifest beneath the ignored
`artifacts/IgnMetadata/1.0.1` directory.

Every generated package includes Jeshibu's MIT license. Compiled packages are
published as GitHub Release assets, never committed to normal Git history.
