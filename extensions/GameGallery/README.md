# Game Gallery

Game Gallery is a standalone Osiris enhancement that displays Steam screenshots,
trailers, and local gallery media on the game-details page.

- Extension ID: `GameGallery_8e77fe31-5e62-41e2-8fa2-64844cfd5b6b`
- Version: `2.0.4`
- Module: `GameGallery.dll`
- Install folder: `Data/Extensions/Extras/GameGallery_8e77fe31-5e62-41e2-8fa2-64844cfd5b6b`
- Private data folder: `Data/ExtensionsData/Extras/GameGallery_8e77fe31-5e62-41e2-8fa2-64844cfd5b6b`

The Gallery card and the Gallery section in Edit Game are supplied only when
this extension is installed and enabled. Disabling or uninstalling it removes
those surfaces without changing the user's game media or other Osiris data.
The card header displays only its gallery title, without a screenshot count.
The extension icon uses the user's supplied Game Gallery PNG unchanged,
including its coloured background.

Version 2.0.3 retains the restored extension settings surface and stable
Osiris presentation fields used to build the
large media stage and the 174-pixel right-side screenshot/trailer rail.

General settings include **Choose expanded experience**. **Full screen** remains
the default for existing profiles. **Cinematic** uses a centered media-only
viewer with a dimmed background, equal padding, and a frame fitted to the current
image or video's aspect ratio. There is no title bar. Images and videos have a
background-free close icon, shared side chevrons identical to the card arrows,
and a bottom-center `1 / 5` counter at 19 px (three points larger than before);
controls hide after pointer inactivity and reappear on
interaction. The Osiris theme supplies the modal frame and video integration;
the extension stores the preference through its normal Save/Cancel transaction.
The image navigation presentation is shared with Screenshots Gallery in both
expanded modes, through `GalleryExpandedWindowPresentation` in the theme.

Build a distributable package from the repository root:

```powershell
.\build\Build-GameGallery.ps1
```

The `.pext` package and SHA-256 manifest are written beneath
`artifacts/GameGallery/2.0.4` and are excluded from Git. Compiled packages belong
in GitHub Release assets.

Game Gallery is an Osiris-maintained fork of darklinkpower's MIT-licensed Steam
Store Screenshots Viewer. See `LICENSE` and `UPSTREAM.md`. Its unused inherited
AngleSharp dependency was removed before the public 2.0.1 release.
