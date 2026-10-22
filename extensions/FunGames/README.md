# Fun Games

Original Osiris extension, version 0.3.2. Requires Osiris Beta 0.0.53 or newer
for complete nested-page Back navigation.

Adds a Fun Games main page through the same Osiris global-page contract as
Exophase and HowLongToBeat. The page is an expandable game-selection hub with
one entry, Roulette. The 360x560 portrait card contains a decorative eight-sector
wheel using a random selection of locally available game heroes, a pastel-purple
background and purple rim/pointer/hub. ROULETTE is integrated in the poster
using Segoe UI Black Condensed display lettering. The wheel hub uses Osiris's
white app mark, embedded from the existing app Images/applogo.png asset as
osiris-mark.png; this is first-party Osiris branding, not game-library artwork.
The full-width grey Play button below the poster has square top/rounded bottom
corners and reuses exact canonical Extensions BrowsePrimary/BrowseInstallPrimary styles
in ExtensionActions.xaml (validated against ThemeSource/Views/MainWindow.xaml).
Play opens a working Roulette main page with a horizontal title-card reel, a
fixed centre marker, a spin action and a winner popup with confetti.
There are no wagers, game launches, library edits, network requests or tracking
changes. Artwork sampling reads
only non-hidden library entries with local background art, decodes at most eight
600px-wide images, skips missing/unsupported files and releases them on unload.
Images remain runtime-only; no library artwork or paths enter source or packages.
Libraries without artwork use a safe decorative gradient wheel.

Extension source is original work created for Osiris, licensed under MIT (see
LICENSE). The user-supplied joystick.svg is a Lucide icon, preserved in assets
and rendered in white over an original muted yellow/gold gradient. The Lucide
ISC notice is in LUCIDE-LICENSE.txt and bundled in every package. License source:
https://github.com/lucide-icons/lucide/blob/main/LICENSE. No recovered binaries.
Playnite.SDK is a compile-time host reference and is not redistributed.

Run build.ps1 to render the SVG into the 128px icon and build Release, then package.ps1
for a privacy-safe .pext and SHA-256 manifest under ignored artifacts/FunGames.
Deploy only the package program files to Development Data/Extensions/Extras/
FunGames_9ac287c1-0c9c-4453-b668-9857c4e7d7e6. Preserve all existing Data.
Restart Development via its root Osiris.exe wrapper; do not launch the engine
directly or touch the personal installation. No public catalog/release promotion
is performed without an explicit release request. Version 0.3.2 was published
with Osiris Beta 0.0.53 on 22 October 2026 after clean/upgrade qualification.

Validation: build `validation/Osiris.FunGames.Validation.csproj` in Release,
then run its executable with `source` and the authoritative MainWindow.xaml
paths (1,436 isolated WPF/model checks). Coverage includes global-page discovery,
icon/template loading, portrait geometry, one-option hub, Play/Back routing,
canonical button-style equivalence, wedge geometry, bounded/repeated hero images,
fallback drawing, unload cleanup, native header sizing, narrow/wide layout, and
absence of game launch/tracking hooks. Tests inspect drawing commands without
taking screenshots or moving the user's mouse.

Manage Roulettes offers Import roulette and Export selected roulette. Portable
`.roulette.json` files contain schema version 1, the roulette name and option
names/descriptions only, without machine-local game/roulette IDs. Imports are
limited to 1 MB, validate schema/depth and normal option limits, and open as a
new builder draft for review and explicit Save. Existing duplicate names are
rejected by the normal save validation; import never silently replaces a save.
Export also supports the temporary Random games roulette and uses the native
save dialog's overwrite confirmation. Import/export cancellation changes nothing.

## Roulette

Version0.3.1 centres the reel in the main page, with Spin beneath its right edge.
The old sidebar is replaced by a top toolbar: Roulette title, outlined selector
of the default/custom saved roulettes, then Manage Roulettes popup. Selection
immediately applies the corresponding options. Manage offers Add/Edit/Delete;
Delete asks for confirmation, rejects the default ID, and preserves .bak before
updating only the chosen saved record. Cancel, Escape and Back dismiss the
confirmation without writes. Existing Add/Edit Save/Cancel remains staged.
Inline Result is hidden; the winner popup and confetti remain unchanged.
Dropdown geometry/template is copied from the canonical settings outlined style,
without implicit host-style dependencies so isolated extension hosts can load it.

Version0.3.0 supersedes all circular-wheel design notes below. The active game
is now a horizontal Counter-Strike-style reel of title cards with coloured
accent strips, equal10px gaps, faded edges and a fixed centre marker. Cards
scroll rapidly then ease to a stop over5.2seconds. The centred card is the
winner shown in the existing popup/confetti. No wagers, currency, purchases,
rarity odds or game launching are involved.

RouletteWheel remains the type name for XAML compatibility, but Position now
uses card units. LandingPosition advances at least30 cards and five complete
cycles, then lands exactly on the securely/uniformly selected index. Card pitch
adapts to viewport width; resizing cannot change the winner. Only nearby cards
are drawn, with repeated options mapped correctly on either side. The builder,
saved JSON schema, default eight games and navigation are unchanged.

Version0.2.7 replaces the top-three aperture with an upper semicircle and a white
perimeter outline. A flat dark white-outlined centre at its baseline restores
the actual Osiris logo. The surrounding roulette frame/window is removed;
static shafts remain directly behind the wheel with radial opacity feathering
so their ends and sides blend into the background. Wheel proportions, constant
gutters, title direction, fair spins, saves and winner celebration are retained.

Version0.2.6 adds symmetric side gradient shadows inside the arc, leaving the
winning centre clear. The wheel has a rounded violet-to-teal frame, inner
hairline, dark surface and static light shafts/glows behind it. The saved-list
panel and gradient Spin action share the new palette and retain keyboard states.
All decorative layers are non-hit-testable; the atmosphere draws seven shapes
without timers, image assets or continuous rendering. Builder controls, data,
probabilities, winner landing and celebration behaviour are unchanged.

Version0.2.5 uses a fixed cropped top-arc aperture spanning three sector widths,
capped at the top180degrees for very small option counts. The full wheel rotates
behind this aperture; its hub and lower sections are hidden. The pointer still
selects the central top sector using the original fair landing calculation.
Circular shapes, constant-width gutters, radial labels, saved definitions and
winner/confetti popup are unchanged. The viewport fits width and height without
squashing the wheel; no extra images or gameplay mechanics are introduced.

Version0.2.4 displays the winning name and description in a centred in-page
popup when the wheel stops. A64-particle vector confetti animation runs behind
the card for2.8 seconds and stops automatically; the winner remains visible
until dismissed. Close, Escape or search-panel Back closes the popup first.
Underlying Roulette controls disable while it is open, Tab remains within the
popup, and focus returns after dismissal. Navigating away stops confetti and
clears the overlay. This is a transient overlay, not a native Middle Window;
there are no global render subscriptions, timers, game launches or new saved
records. Existing result text remains available after closing.

Version0.2.3 uses constant-width6px gutters, not angular gaps: each straight
sector side is offset3px parallel to its boundary ray, with exact intersections
on the inner/outer circles. Tests check equal gap width at the hub and rim for
2/8/32 sectors. Dense/small wheels reduce the width only enough to avoid collapsed
sectors; the resulting width stays constant throughout each gutter.

Version0.2.2 supersedes the segment decoration below: the working wheel now
contains titles only over the glass gradients, without images, stars or centre
logo. Separator edges are straight radial lines. Single-line titles run outward
along each centre-to-rim ray with font fitting and ellipsis for long names.
The heading above the wheel is removed; maximum wheel dimensions increase to
920px and the radial inset shrinks to38px. Spin sits in the wheel column,
measured to share its horizontal centre. The Fun Games poster is unchanged.
No gameplay images are decoded anymore; stored game IDs/roulettes stay intact.

Version0.2.1 places the saved-list/results panel on the left and wheel on the
right. The active wheel uses separated rounded annular glass sectors with
translucent colour gradients and soft sheens, small game-art medallions (star
fallback), a larger opal Osiris hub, and a glowing hollow inverted triangle.
Segment labels follow their sector angle through the complete spin; no horizontal
black label boxes remain. This is drawn as WPF vector geometry, not a screenshot.

There is no duplicate Back button in the content. The search-panel Back uses
the new host extension-subpage contract: public bool OsirisTryNavigateBack().
FunGamesPage consumes Back to cancel the builder first, then return to the hub.
At the hub it returns false so normal history proceeds. Hidden extension views
cannot intercept Back. This needs the matching Development theme update;
see the app repository docs/EXTENSION-GAME-NAVIGATION.md. No coins, paid spins,
daily limitations, countdowns or reward mechanics are copied from the reference.

The default Random games wheel is generated from eight shuffled non-hidden
library games each time the Roulette page opens. If fewer than eight exist,
explicitly labelled genre samples fill the remaining places. This temporary
default does not write to disk. Its segments can show available local hero art;
custom text options use coloured sectors. The Osiris logo remains at the hub.

Spin chooses each option with equal probability using cryptographic rejection
sampling, then animates five or more turns with an eased stop. The selected
sector centre aligns exactly under the top pointer. Switching/building is locked
during a spin. Back or leaving the page stops animation without a delayed result.
No winning game is automatically opened, launched or marked as played.

Build a roulette opens an inline builder with a roulette name and 2–32 options.
Every option has a required name and an optional description. Add/Remove only
change the draft. Save validates and persists; Cancel discards. Saved roulettes
are selectable and editable. Editing Random games creates a separately named
saved copy. Names are limited to80 characters, descriptions to1,000 characters,
and the collection to100 saved roulettes. Duplicate roulette names are rejected.

Data is stored only in the SDK-provided extension-private directory under
`Data/ExtensionsData`, in `roulettes.json` (schema1). Stable GUIDs identify saves;
JSON stores names, descriptions and optional local game IDs, never hero files,
absolute media paths or credentials. Writes use atomic replacement and keep a
`.bak` previous copy. An unreadable/corrupt file is preserved and saving is
disabled with a visible explanation; it is never silently reset. Save errors
keep the builder draft open. The package contains program files and licenses
only, never saved roulettes or library content.

Builder controls reuse the actual Backup-compatible action and input styles
from the authoritative MainWindow.xaml, copied into RouletteControls.xaml.
The inline builder uses40px margins, 760px form, 24px option gaps, and86px
Save/Cancel footer. Multiline description height is a functional input exception.

The current Play variant keeps the browse button geometry but uses 22px text,
grey #303030 background/light text at rest, white/dark on hover and grey/dark on press.
The wheel uses uniform width-based scaling with adaptive poster height rather
than independent X/Y scales; three aspect ratios verify circles and the Osiris
mark cannot be stretched. Poster typography and all image wedges share that
uniform transform; wheel and pointer remain anchored together.
The latest composition enlarges the wheel radius by 10% (240 to 264 design px)
and moves its centre up18px, reducing the empty space above the rim without
moving the poster title or altering circular scaling.
The initial four-file package was SHA-256 and privacy-inventory checked and
extracted into CleanInstall/ExtensionChecks before Development deployment.
The joystick revision includes a fifth file, Licenses/Lucide-License.txt, and
was checked and extracted again. New-Icon.ps1 preserves the supplied 24px SVG
geometry and round strokes, centred on the 128px rounded tile with a muted
yellow/gold #A1872C to #5A4B1C gradient.
Startup confirms Fun Games 0.3.1 loads successfully. The personal installation
is outside scope and unchanged.
