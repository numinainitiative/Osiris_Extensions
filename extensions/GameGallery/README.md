# Game Gallery

Game Gallery is a standalone Osiris enhancement that displays Steam screenshots,
trailers, and local gallery media on the game-details page.

- Extension ID: `GameGallery_8e77fe31-5e62-41e2-8fa2-64844cfd5b6b`
- Version: `2.0.1`
- Module: `GameGallery.dll`
- Install folder: `Data/Extensions/Enhancements/GameGallery_8e77fe31-5e62-41e2-8fa2-64844cfd5b6b`
- Private data folder: `Data/ExtensionsData/Enhancements/GameGallery_8e77fe31-5e62-41e2-8fa2-64844cfd5b6b`

The Gallery card and the Gallery section in Edit Game are supplied only when
this extension is installed and enabled. Disabling or uninstalling it removes
those surfaces without changing the user's game media or other Osiris data.

Version 2.0.1 restores the stable Osiris presentation fields used to build the
large media stage and the 174-pixel right-side screenshot/trailer rail.

Build a distributable package from the repository root:

```powershell
.\build\Build-GameGallery.ps1
```

The `.pext` package and SHA-256 manifest are written beneath
`artifacts/GameGallery/2.0.1` and are excluded from Git. Compiled packages belong
in GitHub Release assets.

Game Gallery is an Osiris-maintained fork of darklinkpower's MIT-licensed Steam
Store Screenshots Viewer. See `LICENSE` and `UPSTREAM.md`.

The inherited source currently relies on AngleSharp 0.9.9, which NuGet flags for
a moderate-severity advisory. The private prerelease retains it for binary
compatibility with the current Osiris runtime; it must be upgraded together with
the corresponding Osiris runtime dependency before public distribution.
