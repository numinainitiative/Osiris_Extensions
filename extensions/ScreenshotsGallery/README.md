# Screenshots Gallery

Original Osiris extension, identity
`ScreenshotsGallery_f3dc3fd5-3d6d-4aa0-8762-2d325bb1d7fe`.
Initial public release 0.1.0. Requires Osiris Beta 0.0.50 or newer for the
game-details card, expanded presentation and per-game editor integration.

While exactly one game is running through Osiris, collect newly saved Windows
Win + Print Screen PNGs into the active profile's
`library/files/<game GUID>/Screenshots` folder. Resolve the Windows Screenshots
known folder through the Shell API, respecting redirects and OneDrive.
Keep the Windows original. Never hook keys, capture the desktop, modify the
clipboard, import old screenshots, or guess between multiple running games.
Osiris must be running and tracking the game session.

Polling waits for an unchanged file and validates its image before copying.
Collection stops when the tracked session ends. The new Game Details card,
below Game Gallery, stays hidden until screenshots exist and refreshes automatically.
The card uses Game Gallery's exact 520-pixel content height, margins, large-image
stage, 190-pixel thumbnail rail, rounded corners and shared arrow controls.
The newest screenshot is selected first; its capture date overlays the picture's
bottom-left corner, including in the expanded viewer. Left/right controls and
the thumbnail rail browse the complete collection. Click the main image for the
in-app viewer, with arrow-key navigation and Escape to close. Extension settings
select Full screen or Cinematic; Cinematic reuses Game Gallery's theme presentation.
Both expanded modes use the same background-free side chevrons (identical to
the card arrows), bottom-center `1 / 5` counter at 19 px, and close icon as
Game Gallery, hiding after 2.2 seconds of pointer
inactivity. The mode is captured at click time before asynchronous image loading;
the theme honors that captured window mode. The settings object is registered
through the same SDK settings support contract as Game Gallery.
Open folder is a text action at the right of the card header and shares Game
Gallery's darker secondary header colour (`#484848`). Collapsed cards retain
the same 10-pixel lower inset as Game Gallery; spacers are transparent to preserve
the rounded outer outline. Expanded images use uniform scaling, not cropping.
Originals remain unchanged and are not held open.

Run `build.ps1` or `package.ps1`. No personal profile or screenshots are packaged.

Game Edit -> Extensions -> Screenshots Gallery provides a per-game enable
switch and a full-width image gallery with six thumbnail columns. Hovering a
thumbnail shows a red cross in its top-right corner to stage individual deletion.
Dates remain below thumbnails; images use uniform scaling and bounded decoded
previews without locking their files. The Backup-derived switch, text and footer
styles are retained. The user-requested full-width gallery replaces the canonical
760-pixel table layout specifically for this image-management surface.
The existing game-edit Save/Cancel footer owns the transaction: changes are
staged until Save; Cancel leaves settings and files untouched. Saved deletions
move only that game's direct PNG copies into `Screenshots/Deleted` for recovery;
Windows Pictures originals are never changed. The per-game switch is persisted
in `OsirisScreenshotsGallery.ini` alongside the game's files. Disabling stops
collection for that game and hides its gallery without removing screenshots.
The extension icon is the user's supplied Screenshots Gallery PNG from the
graphics workspace, copied unchanged, including its coloured background.
The original Lucide Fullscreen SVG is retained as a reference asset.
Lucide artwork is ISC licensed: https://lucide.dev/license
Windows known folder reference:
https://learn.microsoft.com/en-us/windows/win32/shell/knownfolderid
