# Steam Library

- Identity: `SteamLibrary_cb91dfc9-b977-43bf-8e70-55f46e410fab`
- Osiris version: `1.0.1`
- Upstream code baseline: Steam Library `2.40`
- Status: private Osiris development fork; distributed only through a private
  prerelease while final regression and dependency work continues

Steam Library imports locally installed and account-owned Steam games into
Osiris. The source is based on the exact upstream revision used by the former
installed `2.40` binary, with Osiris-specific packaging and fixes maintained in
this repository.

## Osiris changes

- Resolve vertical covers from Steam's declared
  `common/library_assets_full/library_capsule` metadata.
- Prefer the language-specific 2x asset, then English, another available
  language, the standard-resolution asset, and finally the legacy Steam CDN
  route.
- Preserve the Steam Library plugin GUID so existing imported games remain
  associated with the extension.
- Present the Steam authentication browser as an Osiris middle window while
  preserving Steam's login, cookie, token, and automatic-close behavior.
- Remove the obsolete tag-blacklist setting and its metadata-import filtering.
- Keep the tag-count input editable independently from its enable switch.
- Align the extension's settings markup with Osiris's hosted settings and
  managed-list contracts.

This fixes games whose legacy CDN cover was 600x800 and therefore cropped in
Osiris's 2:3 vertical grid. Steam's declared library capsule assets are 2:3
(normally 600x900 at 2x resolution).

## Build

Run `build.ps1` from PowerShell. The script restores the pinned NuGet packages
outside Git and invokes Visual Studio MSBuild. Build output is written beneath
the ignored `source/Libraries/SteamLibrary/bin` directory.

Run `package.ps1` to rebuild and create the installable
`SteamLibrary_1.0.1.pext` archive plus its SHA-256 JSON manifest beneath the
ignored `artifacts/SteamLibrary/1.0.1` directory.

See `UPSTREAM.md` and `LICENSES/` for provenance and licensing.

## Public-release hold

The exact 2.40 baseline pins AngleSharp 0.9.9 and Newtonsoft.Json 10.0.3.
Current NuGet auditing flags security advisories for both inherited versions.
Steam Library must remain private until those dependencies are upgraded and the
authentication, account import, metadata, install, and launch paths are
regression-tested.
