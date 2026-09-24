# Stats

Stats is an Osiris global-page extension. It owns the Stats entry shown in the
installed extensions section of the navigation panel. The page is split into
independently collapsible Weekly Stats, All Time Stats, and How Long To Beat
sections, plus an Exophase section when Exophase is installed. Extension-owned
sections are discovered through the extension's live plugin instance: a section
appears only while its extension is loaded and disappears when that extension
is removed or unavailable.

Weekly Stats is one exact ten-unit row. It shows time played in the same
seven-day bar treatment as Home, new games, favourite play time, average
session length, trophies earned, and the most-played game. Weekly playtime is
read from Home's profile-local daily ledger and falls back to sessions observed
by Stats when that ledger has no entries. Because Exophase exposes a lifetime
trophy total rather than individual award timestamps, the weekly trophy tile
stores a profile-local baseline at the first refresh of each week and reports
subsequent increases.

All Time Stats shows owned, installed, played, and unplayed games; unified
total played time; total play count; earned Exophase trophies; installed
library size; saved game collections; gaming days; average and longest
sessions; longest gaming streak; favourite play time; profile age; and oldest,
newest, and heaviest game highlights. It also includes the largest saved
collection, the developer whose visible games have the greatest combined
playtime, and a completion-status summary. Unified playtime uses Exophase's
resolved cross-platform total for matched library games, matching the total
shown on Exophase's global page; native playtime is used only when Exophase is
unavailable.

Four list panels show top-played games, last-played games, libraries ordered by
game count, and the completion-status summary. Every list is capped at five
entries and contains no nested scrolling, so the mouse wheel always controls
the Stats page. Their
content area is divided into five equal 82.6 px rows. Game names truncate before
a reserved 100 px detail column, whose 20 px type matches the game-title type.
Game lists use square library icons. The Libraries list uses the same platform
vectors as Exophase inside monochrome outlined tiles, while locally added games
use the Osiris logo and the name Osiris instead of Manual. View More opens the
Library. Favourite games are represented by a single-unit `FAV. GAMES` card,
hidden games by a single-unit `HID. GAMES` card, and installed size ranking by
a single Heaviest Game highlight card. The first ten-unit line is ordered as
Owned Games, Total Played Time,
Played Games, Total Play Count, Top Played Games, Game Collections, and Games
Not Played. Every list panel has a true three-unit footprint: 482 x 503 px,
equal to three 150 x 157 px cards plus both intervening 16 px gaps. All other
cards retain their established 150 px single-unit size. Double-unit cards are
316 px wide, exactly two 150 px units plus the intervening 16 px grid gap.
Average Session, Longest Session, and Library Age also use double units because
their values can contain two numeric parts. Gaming Days and streaks use compact
`D` notation, and Library Age uses `Y`, `M`, and `d` notation. The remaining
statistics and lists currently flow through the mosaic below and can be refined
independently.

Primary card values use 36 px type. Card labels use 16 px type so longer labels
require less Viewbox reduction. Game, collection, and developer highlights all
share one fixed 23 px style on one line and truncate with an ellipsis rather
than changing size based on name length.
The Osiris mark receives a local scale correction inside the Libraries icon
tile to compensate for the source image's transparent margin. Right-side list
details have an additional 8 px inset from the list edge.
List headers and View More actions use the same 16 px type as ordinary card
labels, with View More rendered in the darker `#444444`; row ranks use 19 px,
and row titles and right-side values use 20 px. Status List follows the same ranked,
outlined-icon-tile, title, value, and header/action structure as Libraries.

How Long To Beat begins with a row containing single-unit With Data and Without
Data cards followed by double-unit total Main Story, Main + Extras, and
Completionist-hour cards. Beneath it, three true three-unit
lists rank the five longest visible games for Main Story, Main + Extras, and
Completionist. All values use HowLongToBeat's currently selected Rushed,
Average, Median, or Leisure profile and the same saved manual/automatic result
selection as its global page. This dashboard bridge reads only values already
stored by HowLongToBeat and never performs a network fetch. Hidden games are
excluded consistently with the rest of Stats; disabled games or games without
a positive value in the selected profile count as games without data.

Exophase begins with single-unit With Data and Without Data cards, a double-unit
Most Played Platform highlight, and a single-unit 100% Games card. Its
three-unit Played Platforms list ranks the five platforms with the most
resolved playtime and shows both each platform's share of the aggregate and its
hours/minutes. Platform names and vectors use the same canonical visual catalog
as Exophase. The section reads one in-process, read-only snapshot from
Exophase's existing resolver, so it inherits the extension's ambiguous-title
guards, manual links, native-platform baseline handling, and prevention of
double-counted current-platform time. Hidden games are filtered by Stats after
the snapshot is read.

Each category heading has its own right-aligned grey `Edit` action. Pressing it
places only that category in edit mode and changes its text to `Save`; pressing
it again persists that category's arrangement and exits edit mode. Switching
directly to another category saves the active category first. Every statistic
and list panel moves on its category's fixed magnetic ten-column grid rather
than by free pixel placement. Horizontal slots
are 150 px wide with a 16 px gutter (166 px per step), and vertical slots are
157 px tall with a 16 px gutter (173 px per step). A drop always resolves to
whole grid units. When a compatible occupied footprint is targeted, the
displaced panel or group moves into the vacated cells; a drop that cannot be
resolved without overlap snaps back to its origin. Moves remain provisional
until `Save` is pressed; leaving the page during edit mode restores the
last saved arrangement. Positions are profile-local in
`Settings/Osiris/stats-layout.json`, so personal layouts never enter the
extension package or repository. Older All Time-only layout files remain valid;
the two newer categories keep their defaults until the first explicit save.
Every category after the first uses one shared 42 px heading margin, matching
the accepted Weekly Stats-to-All Time Stats separation regardless of the
category's own canvas height or installed-extension visibility. Category
canvases shrink to the actual bottom edge of their lowest occupied panel after
loading, moving, saving, or cancelling a layout, so reorganizing a shorter
dashboard cannot leave unused magnetic rows before the next heading.

Osiris stores lifetime playtime but not complete historical sessions. Stats
therefore records completed sessions from the moment this version is active in
its profile-local extension data. Longest Session uses only those observed
sessions and displays an em dash until one exists. Gaming days and streaks
combine observed sessions, the existing Home daily playtime ledger, and known
last-played dates. Average Session falls back to native lifetime playtime
divided by native play count until an observed session exists. Favourite time
falls back to known last-played timestamps. The page refreshes when games,
collections, or the session ledger change.

Its cards intentionally share the Game Details statistics palette: `#070707`
shell and gaps, `#282828` outline, `#111111` value surface, `#0B0B0B` label
surface, `#F2F2F2` primary values, `#626262` labels, and `#C8C8C8`
secondary text.

## Development build

```powershell
.\build.ps1
```

The supplied `source/icon.png` is the extension's icon in the installed list
and global-page navigation. The build copies this file unchanged; it does not
generate a replacement icon.

Stats is intentionally not listed in the public extension catalog yet.
