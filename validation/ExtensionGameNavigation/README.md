# Extension game-title navigation regression checks

This Windows/.NET Framework 4.6.2 WPF fixture loads the actual extension DLLs
and Osiris theme navigation bridge. It invokes each global page's title-click
handler against a small in-memory view model and a bound Details text control.
The fixture models the native two-way selection resets during Library/Details
activation, including a deferred data-binding pass. No window is shown, no live
API/database is connected, and no network requests are made.

From the Osiris workspace root, build both extensions and their existing
validation projects first, then run:

```powershell
dotnet build GitHub/Extensions/validation/ExtensionGameNavigation/ExtensionGameNavigation.csproj -c Release
& GitHub/Extensions/validation/ExtensionGameNavigation/bin/Release/net462/ExtensionGameNavigation.exe `
    Programming/Development/Osiris/App/OsirisTheme.dll `
    Programming/Development/Osiris/App `
    GitHub/Extensions/extensions/Exophase/source/bin/Release/net462/Osiris.Exophase.dll `
    GitHub/Extensions/extensions/HowLongToBeat/source/bin/Release/net462/Osiris.HowLongToBeat.dll
```

All four paths may instead point to an isolated candidate runtime and candidate
DLLs. This test requires an Osiris theme with the public HeaderNavigation bridge.

The 30 checks cover both extensions: handled title clicks, native Library and
Details activation, page detachment, selected-game and Details binding survival,
the actual global-page opening command, Back restoring the native extension
content (not just its heading), immediate Back before deferred bindings,
repeated round trips without duplicate history, leaving Details before the
queued refresh, rapid successive game selection, and missing rows leaving the
current selection unchanged.

For an old DLL pair, append `--expect-blank` to verify the original failure:
the correct heading/navigation changes while the Details selection is empty
after rebinding. This mode has 12 checks and is expected to fail for fixed DLLs.
With fixed extension DLLs but the old theme, `--expect-back-blank` instead
reproduces Back updating the heading without reopening extension content in
26 checks. Both baseline switches are intentionally incompatible with the
corresponding fixed implementation.
It is a controlled regression fixture, not a claim of visual acceptance in the
running application; the user confirms both live pages in Development.
