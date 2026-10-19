# Game Gallery icon

The current symbol was supplied by the workspace owner on 2026-10-19 from
the supplied `gallery.png`. Its unchanged bytes are preserved
in `source/gallery-symbol.png`. `Icon.xaml` uses its alpha channel as the mask
for a white symbol over the accepted purple-gradient tile, retaining the
Trophies-style 64-unit grid and 10-unit rounded tile corners. No symbol geometry
is redrawn. On 2026-10-19 the workspace owner confirmed they created this symbol
in Photoshop and requested its inclusion in the coordinated public release.

Run `Build-Icon.ps1` in Windows PowerShell STA mode to render `source/icon.png`.
The catalog and Development theme use the same generated PNG; no extension
assembly rebuild is required. SVG preview wrappers reference the generated PNG;
the editable composition is XAML. The earlier owner-supplied 3000px purple artwork
is superseded, not deleted from the Graphics workspace.
