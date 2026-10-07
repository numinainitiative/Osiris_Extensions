Trophies 0.1.0

Initial Osiris release. Requires Osiris Beta 0.0.51 or newer for its Game Details and Edit Game integration.

- Imports Steam achievement catalogues, icons, hidden flags and optional global rarity percentages using a saved Steam Web API key.
- Shows three trophies on Game Details, prioritising latest unlocks and rarity for exact-time ties; View All opens the grouped full list with Title, Rarity and Last achieved sorting.
- Uses purple for the rarest trophy, gold for rarity ranks 2-5 and silver for ranks 6-10, with subdued grey halos while locked.
- Keeps hidden descriptions concealed until clicked in View All. Full view retains descriptions beside unlock dates and earned platforms.
- Local games reuse their saved Exophase editions through one manual Sync action. Requires Exophase 0.2.7; no second URL or edition selection is needed in Trophies.
- Integrated Steam games use the connected Steam account; integrated Xbox games use the Xbox Library 1.0.2 achievement bridge. Integrated games never fall back to Exophase.
- General settings contain the card visibility switch. Account contains the masked, Windows-account-encrypted Steam Web API key. Fetch catalogue remains staged for Edit Game Save/Cancel.

No local in-game achievement detector or notification system is included. Steam sync was tested against an authenticated Development account. Xbox still needs live-account acceptance testing. Exophase website access restrictions, missing links and unmatched port-specific achievements fail safely without inventing unlocks or erasing cached results.

Original MIT-licensed source. Steam/Exophase catalogue content is fetched at runtime; no user credentials, library, browser session, cached catalogue or downloaded trophy icons are packaged.
