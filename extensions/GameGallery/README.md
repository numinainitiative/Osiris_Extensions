# Game Gallery

Game Gallery is a standalone Osiris enhancement that displays Steam screenshots,
trailers, and local gallery media on the game-details page.

- Extension ID: `GameGallery_8e77fe31-5e62-41e2-8fa2-64844cfd5b6b`
- Version: `2.0.2`
- Module: `GameGallery.dll`
- Install folder: `Data/Extensions/Extras/GameGallery_8e77fe31-5e62-41e2-8fa2-64844cfd5b6b`
- Private data folder: `Data/ExtensionsData/Extras/GameGallery_8e77fe31-5e62-41e2-8fa2-64844cfd5b6b`

The Gallery card and the Gallery section in Edit Game are supplied only when
this extension is installed and enabled. Disabling or uninstalling it removes
those surfaces without changing the user's game media or other Osiris data.

Version 2.0.2 restores the extension settings surface and retains the stable
Osiris presentation fields used to build the
large media stage and the 174-pixel right-side screenshot/trailer rail.

General settings include **Choose expanded experience**. **Full screen** remains
the default for existing profiles. **Cinematic** uses a centered media-only
viewer with a dimmed background, equal padding, and a frame fitted to the current
image or video's aspect ratio. There is no title bar. Images and videos have a
circular close button; controls hide after pointer inactivity and reappear on
interaction. The Osiris theme supplies the modal frame and video integration;
the extension stores the preference through its normal Save/Cancel transaction.

Build a distributable package from the repository root:

```powershell
.\build\Build-GameGallery.ps1
```

The `.pext` package and SHA-256 manifest are written beneath
`artifacts/GameGallery/2.0.2` and are excluded from Git. Compiled packages belong
in GitHub Release assets.

Game Gallery is an Osiris-maintained fork of darklinkpower's MIT-licensed Steam
Store Screenshots Viewer. See `LICENSE` and `UPSTREAM.md`. Its unused inherited
AngleSharp dependency was removed before the public 2.0.1 release.
