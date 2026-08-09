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
Store Screenshots Viewer. See `LICENSE` and `UPSTREAM.md`. Its unused inherited
AngleSharp dependency was removed before the public 2.0.1 release.
