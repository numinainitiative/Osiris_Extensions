# Steam Metadata upstream provenance

- Upstream repository: `https://github.com/JosefNemec/PlayniteExtensions`
- Upstream component: the Steam metadata provider now shipped by Osiris as
  Steam Metadata
- Upstream license: MIT, copyright 2020 Josef Nemec
- Osiris baseline: the installed Steam Metadata 1.0 assembly was
  decompiled with its matching PDB and checked against the official upstream
  implementation before migration.

The Osiris fork preserves the extension ID and native Steam metadata download,
artwork, tag, language, Steam Deck compatibility, and settings behavior. Its
user-facing provider name is now Steam Metadata.

## Bundled SteamKit2 dependency

Steam Metadata dynamically loads SteamKit2 1.8.3. The audited upstream tag is
`1.8.3` at commit `4eb340ecbf0686003ae3fa02296ce435e295ae5e` in
`https://github.com/SteamRE/SteamKit`. SteamKit2 is licensed under LGPL 2.1 or
later, copyright 2017 Ryan Stecker and the SteamRE Team. Its upstream notice and
the complete LGPL 2.1 terms are stored in `LICENSES/` and included in every
generated package.
