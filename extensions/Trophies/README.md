# Trophies 0.1.0

Original MIT-licensed Numina Initiative extension. Requires Osiris Beta 0.0.51
or newer for its Game Details card and Game Edit integration.

## Setup

1. Install Trophies from Extensions after updating Osiris.
2. In Trophies settings -> Account, enter your Steam Web API key and Save.
3. In Game Edit -> Extensions -> Trophies, choose a Steam App ID/store URL and
   Fetch catalogue. Verify the imported game name; Save applies the catalogue,
   while Cancel leaves the previous catalogue intact.
4. For local games, synchronise your Exophase profile and save the intended
   edition(s) in Game Edit -> Exophase. Press Sync in Trophies to update earned
   trophies immediately. This action is not staged for Edit Game Save/Cancel.

General settings contain only Show Trophies cards on Game Details. Account
contains the masked Steam API key and catalogue explanation. The key is encrypted
with Windows DPAPI for the current Windows account; re-enter it when moving to
another Windows account. No global Exophase switch is required.

## Unlock providers

The game's integration ID, not its editable Source label, selects the provider:

- Local games: manual per-game Sync through Exophase 0.2.7. The read-only,
  versioned `GetUnlockedAchievementsForOsiris(Guid, CancellationToken)` bridge
  supplies verified individual awards, UTC dates and platforms from saved
  editions. Trophies does not independently access the Exophase website or ask
  for another URL/edition selection. Opening Game Details does not sync local
  trophies automatically.
- Steam Library games: Valve's GetPlayerAchievements for the connected primary
  account and exact integration App ID. Steam Library 1.0.3 is current. Native
  sync currently prefers a saved Trophies API key over the integration key;
  the key is therefore used for this read-only native request too.
- Xbox Library games: Xbox Library 1.0.2's authenticated read-only bridge resolves
  the title ID and queries the official Xbox achievement service.
- Other integrations: no native trophy provider yet. Integrated games never
  borrow Exophase unlocks or fall back to local-game matching.

Native catalogues synchronise at startup, after library-update jobs, and when
their card becomes visible (successful requests are throttled for ten minutes).
Requests/writes are serialized; card cache revisions refresh the display.
Source/account changes, incomplete responses and failures preserve cached state.

Exophase combines verified platform pages for saved editions. Unique exact
achievement names are matched across platforms; ambiguous names and unmatched
port-specific awards remain unmatched. Partial platform failures retain prior
unlocks and produce an editor warning. Profile/game identities are rechecked
before writing. Aggregate completion counts never invent individual unlocks.

Steam native sync was live-validated in Development against Battlefield 2042
(19/34 earned). Xbox still needs live-account acceptance testing. Exophase site
verification/access restrictions can prevent live updates; there is no challenge
bypass. Local in-game unlock detection, notifications, memory inspection and
modification of platform achievement state are not implemented.

## Presentation

Game Details previews at most three trophies, latest unlock first across both
categories. Exact-time ties prefer lower known rarity percentages. Remaining
slots use commonly earned non-hidden achievements, with title ordering when
rarity is unknown. Earned icons are coloured; other icons are grayscale.

View All opens an owner-modal, work-area-clamped window, grouping Achievements
and Hidden. Title, Rarity and Last achieved sorts always retain these groups
and place earned trophies first within each. Unlock dates and earned platforms
share the title size; descriptions remain visible below. Locked hidden
descriptions say Hidden until clicked, with popup-local reveal state.

The single rarest catalogue trophy has a purple halo, rarity ranks 2-5 gold and
6-10 silver. Halos are subdued and grey while locked. Missing rarity never
produces a halo. Percentages are Steam's global earned-player percentages,
not a claim of cross-platform rarity. Re-fetch and Save older catalogues to
obtain percentages.

## Safety and provenance

Catalogue imports are explicit, bounded, serialized and cancellable. Icons use
allowlisted HTTPS Steam CDN URLs with redirects disabled; failures use a
placeholder. Validated catalogue JSON is atomically replaced. Reimports retain
verified unlock provenance. Catalogues remain usable offline in private
ExtensionsData. Credentials, browser state, downloaded content and live data
are never committed or packaged.

Source is newly authored for Osiris; no third-party implementation was copied.
Steam and Exophase content/icons remain third-party content fetched at runtime.
The parser never executes saved HTML or vendors website implementation code.
Canonical Backup resources are reused exactly because the extension cannot
resolve the settings page's local resources directly.

Official endpoints:

- [Steam schema](https://partner.steamgames.com/doc/webapi/ISteamUserStats#GetSchemaForGame)
- [Steam player achievements](https://partner.steamgames.com/doc/webapi/ISteamUserStats#GetPlayerAchievements)
- [Steam global percentages](https://partner.steamgames.com/doc/webapi/ISteamUserStats#GetGlobalAchievementPercentagesForApp)
- [Xbox achievements](https://learn.microsoft.com/en-us/gaming/gdk/docs/reference/live/rest/uri/achievements/uri-achievementsusersxuidachievementsgetv2)

Run `build.ps1` to build, generate the original icon and execute fixture/WPF
validation. `package.ps1` stages build outputs and the MIT license only;
compiled packages belong in GitHub Release assets, not Git history.
