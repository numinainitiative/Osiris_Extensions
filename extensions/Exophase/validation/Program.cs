using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Osiris.Extensions.Exophase;
using Playnite.SDK.Models;

internal static class Program
{
    private static int checks;

    [STAThread]
    private static int Main()
    {
        try
        {
            RunChecks();
            Console.WriteLine($"PASS checks={checks}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL " + exception);
            return 1;
        }
    }

    private static void RunChecks()
    {
        var settings = new ExophaseSettings();
        Expect(!settings.ConnectAccount, "Account connection should require an explicit opt-in.");
        Expect(settings.ImportAchievements && settings.ImportPlayTime && settings.ImportPlatforms,
            "All supported import categories should be selected by default.");
        Expect(settings.SyncOnStartup, "Startup synchronisation should be selected by default.");
        Expect(!settings.OverrideDisplayedPlaytime,
            "Total Time Played should require an explicit display opt-in.");

        settings.BeginEdit();
        settings.ConnectAccount = true;
        settings.ImportPlayTime = false;
        settings.ProfileReference = "TestAgent";
        List<string> errors;
        Expect(settings.VerifySettings(out errors) && errors.Count == 0,
            "Foundation preferences should validate without requiring credentials.");
        settings.CancelEdit();
        Expect(!settings.ConnectAccount && settings.ImportPlayTime &&
               string.IsNullOrEmpty(settings.ProfileReference),
            "Cancelling settings should restore the original values.");

        var settingsView = new ExophaseSettingsView();
        var tabs = settingsView.Content as TabControl;
        var tabItems = tabs?.Items.Cast<TabItem>().ToList();
        Expect(tabItems != null && tabItems.Count == 3,
            "Exophase should expose exactly three settings categories.");
        Expect(string.Equals(tabItems[0].Header as string, "Account", StringComparison.Ordinal) &&
               string.Equals(tabItems[1].Header as string, "General", StringComparison.Ordinal) &&
               string.Equals(tabItems[2].Header as string, "Danger Zone", StringComparison.Ordinal),
            "Account, General, and Danger Zone should use the requested order.");
        Expect(settingsView.FindName("ConnectAccountToggle") is CheckBox &&
               settingsView.FindName("AuthenticateButton") is Button &&
               settingsView.FindName("SignOutButton") is Button &&
               settingsView.FindName("ProfileReferenceTextBox") is TextBox &&
               settingsView.FindName("SynchronizeButton") is Button &&
               settingsView.FindName("OsirisExtensionDangerZone") is Grid,
            "The settings view should expose account, extractor, and danger-zone actions.");

        Expect(ExophaseAuthenticationService.IsAuthenticatedAddress("https://www.exophase.com/account/") &&
               ExophaseAuthenticationService.IsAuthenticatedAddress("https://exophase.com/account/connected-platforms/") &&
               !ExophaseAuthenticationService.IsAuthenticatedAddress("https://www.exophase.com/login/") &&
               !ExophaseAuthenticationService.IsAuthenticatedAddress("https://example.com/account/"),
            "Authentication completion should accept only Exophase account pages.");

        var extensionRoot = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", ".."));
        var viewSource = File.ReadAllText(Path.Combine(extensionRoot, "source", "ExophaseSettingsView.xaml"));
        var authSource = File.ReadAllText(Path.Combine(extensionRoot, "source", "ExophaseAuthenticationService.cs"));
        Expect(viewSource.Contains("Width=\"760\"") &&
               viewSource.Contains("Width=\"44\" Height=\"24\"") &&
               viewSource.Contains("OverrideDisplayedPlaytime") &&
               viewSource.Contains("SyncOnStartup") &&
               viewSource.Contains("Display Total Time Played") &&
               viewSource.Contains("Synchronise the platforms and played time for Osiris games") &&
               !viewSource.Contains("ImportAchievements, Mode=TwoWay") &&
               !viewSource.Contains("ImportPlayTime, Mode=TwoWay") &&
               !viewSource.Contains("ImportPlatforms, Mode=TwoWay") &&
               !viewSource.Contains("Text=\"{Binding SynchronizationStatus}\"") &&
               !viewSource.Contains("Osiris keeps an offline Exophase snapshot") &&
               viewSource.Contains("x:Name=\"OsirisExtensionDangerZone\"") &&
               !viewSource.Contains("PasswordBox"),
            "Settings should expose only the two final preferences, the concise synchronisation action, canonical dimensions, and no password field.");
        Expect(authSource.Contains("CreateView(1225, 700)") &&
               authSource.Contains("https://www.exophase.com/account/") &&
               authSource.Contains("DeleteDomainCookies") &&
               !authSource.Contains("Password"),
            "Authentication should use the Middle Window webview and cookie-only session handling.");

        var settingsSource = File.ReadAllText(Path.Combine(extensionRoot, "source", "ExophaseSettings.cs"));
        var settingsViewSource = File.ReadAllText(Path.Combine(extensionRoot, "source", "ExophaseSettingsView.xaml.cs"));
        var pluginSource = File.ReadAllText(Path.Combine(extensionRoot, "source", "ExophasePlugin.cs"));
        Expect(pluginSource.Contains("public static ExophasePlugin Current") &&
               pluginSource.Contains("GetEffectivePlaytimeForOsiris") &&
               pluginSource.Contains("settings.OverrideDisplayedPlaytime") &&
               pluginSource.Contains("resolvedActivity.CanDisplayTotal") &&
               pluginSource.Contains("return game.Playtime") &&
               pluginSource.Contains("return resolvedActivity.TotalPlaytimeSeconds"),
            "Exophase should expose its guarded effective playtime to optional peer extensions.");
        Expect(settingsSource.Contains("internal async Task AuthenticateAsync()") &&
               settingsSource.Contains("if (IsConnected)") &&
               settingsSource.Contains("await SynchronizeAsync();") &&
               settingsViewSource.Contains("await settings.AuthenticateAsync();") &&
               pluginSource.Contains("await settings.RefreshConnectionStatusAsync();") &&
               pluginSource.Contains("if (!settings.IsConnected)") &&
               pluginSource.Contains("await settings.SynchronizeAsync(false);"),
            "A successful sign-in and an authenticated blank-profile startup should synchronize activity automatically.");

        CheckActivityParser();
        CheckActivityCard(extensionRoot);
        CheckSafeImportContract(extensionRoot);
    }

    private static void CheckActivityParser()
    {
        const string json = @"{
  'success': true,
  'games': [
    {
      'master_id': 101,
      'environment': { 'name': 'PlayStation 5' },
      'playtimeUnits': { 'hours': 34, 'minutes': 30 },
      'earned_awards': 8,
      'total_awards': 12,
      'percent': 66.7,
      'lastplayed_utc': 1700000000,
      'meta': { 'title': 'The Last of Us™ Part I', 'platforms': [ { 'name': 'PlayStation 5' } ] }
    },
    {
      'master_id': 202,
      'environment': 'Steam',
      'playtimeUnits': { 'hours': 19 },
      'earned_awards': 4,
      'total_awards': 10,
      'meta': { 'title': 'The Last of Us Part I' }
    }
  ]
}";
        var wrapped = "<html><body><pre>" +
                      System.Net.WebUtility.HtmlEncode(json) +
                      "</pre></body></html>";
        var unwrapped = ExophaseActivityParser.UnwrapJsonPage(wrapped);
        var games = ExophaseActivityParser.ParseGames(JObject.Parse(unwrapped));
        Expect(games.Count == 2 &&
               games[0].PlaytimeSeconds == (34UL * 3600UL + 30UL * 60UL) &&
               games[0].Platform == "PlayStation 5" &&
               games[1].Platform == "Steam",
            "The extractor should parse platform-specific Exophase playtime records.");

        var aggregates = ExophaseActivityParser.AggregateGames(games);
        Expect(aggregates.Count == 1 &&
               aggregates[0].Platforms.Count == 2 &&
               aggregates[0].TotalPlaytimeSeconds == (53UL * 3600UL + 30UL * 60UL),
            "Equivalent platform titles should aggregate into one cross-platform game total.");
        Expect(ExophaseActivityParser.NormalizeTitle("Pokémon™: Édition") == "pokemonedition",
            "Title matching should normalize punctuation, trademarks, spacing, and accents.");
        Expect(ExophaseActivityParser.FindPlayerProfileId(
                   "<script>window.playerProfileId = 39458;</script>") == "39458" &&
               ExophaseActivityParser.FindProfileSlug(
                   "<a href=\"/user/TestAgent/\">Profile</a>") == "TestAgent",
            "The signed-in browser page should resolve the numeric profile identity.");
        Expect(ExophaseActivityParser.GetProfilePageUrl("Test Agent") ==
               "https://www.exophase.com/user/Test%20Agent/",
            "Usernames should be converted into safe Exophase profile URLs.");
    }

    private static void CheckSafeImportContract(string extensionRoot)
    {
        var pluginSource = File.ReadAllText(Path.Combine(extensionRoot, "source", "ExophasePlugin.cs"));
        var extractionSource = File.ReadAllText(Path.Combine(extensionRoot, "source", "ExophaseExtractionService.cs"));
        Expect(pluginSource.Contains("candidates.Count != 1") &&
               pluginSource.Contains("GetGameSettingsForOsiris") &&
               pluginSource.Contains("SearchGamesForOsiris") &&
               pluginSource.Contains("SaveGameSettingsForOsiris") &&
               pluginSource.Contains("matchedTitle") &&
               !pluginSource.Contains("Database.Games.Update") &&
               !pluginSource.Contains("game.Playtime ="),
            "Exophase should match unique titles and expose per-game settings without overwriting native playtime.");
        Expect(extractionSource.Contains("exophase-raw-latest.json") &&
               extractionSource.Contains("exophase-activity-latest.json") &&
               extractionSource.Contains("File.Replace") &&
               extractionSource.Contains("CreateOffscreenView") &&
               extractionSource.Contains("browser.GetCookies()") &&
               extractionSource.Contains("TryAddWithoutValidation(\"Cookie\"") &&
               extractionSource.Contains("navigator.userAgent") &&
               extractionSource.Contains("fetch(") &&
               extractionSource.Contains("credentials:'include'") &&
               extractionSource.Contains("EnsureExophasePageAsync") &&
               extractionSource.Contains("CanExecuteJavascriptInMainFrame") &&
               extractionSource.Contains("Task.Delay(500, cancellationToken)"),
            "The extractor should atomically cache snapshots and reuse the verified browser session for API requests and verification retries.");
    }

    private static void CheckActivityCard(string extensionRoot)
    {
        Expect(ExophaseActivityControl.FormatCompactPlaytime(0) == "0m" &&
               ExophaseActivityControl.FormatCompactPlaytime(60) == "1m" &&
               ExophaseActivityControl.FormatCompactPlaytime(2UL * 3600UL + 3UL * 60UL) ==
                   "2h 3m" &&
               ExophaseActivityControl.FormatCompactPlaytime(54UL * 3600UL) == "54h",
            "The details card should use the requested compact hours-and-minutes format.");
        Expect(ExophaseActivityControl.FormatDisplayPlaytime(60) == "1 Minute" &&
               ExophaseActivityControl.FormatDisplayPlaytime(2UL * 3600UL) == "2 Hours" &&
               ExophaseActivityControl.FormatDisplayPlaytime(2UL * 3600UL + 3UL * 60UL) == "2h 3m",
            "The optional total should use a compact readable display value.");
        Expect(ExophaseActivityControl.GetSourceTag(new ExophaseResolvedPlatform
               {
                   IsBaseline = true,
                   IsManual = true
               }) == "Local" &&
               ExophaseActivityControl.GetSourceTag(new ExophaseResolvedPlatform
               {
                   IsManual = true
               }) == "Manual" &&
               ExophaseActivityControl.GetSourceTag(new ExophaseResolvedPlatform()) == "Exophase",
            "Platform rows should distinguish native, manual, and synchronized play-time provenance.");
        Expect(!ExophaseActivityControl.HasDisplayableActivity(null) &&
               !ExophaseActivityControl.HasDisplayableActivity(new ExophaseResolvedActivity
               {
                   Enabled = true
               }) &&
               !ExophaseActivityControl.HasDisplayableActivity(new ExophaseResolvedActivity
               {
                   Enabled = false,
                   Platforms = new List<ExophaseResolvedPlatform>
                   {
                       new ExophaseResolvedPlatform { Platform = "Steam" }
                   }
               }) &&
               ExophaseActivityControl.HasDisplayableActivity(new ExophaseResolvedActivity
               {
                   Enabled = true,
                   Platforms = new List<ExophaseResolvedPlatform>
                   {
                       new ExophaseResolvedPlatform { Platform = "Steam" }
                   }
               }),
            "The details card should remain hidden until an enabled game has synchronized or manually added platform activity.");

        var steamVisual = ExophasePlatformVisualCatalog.Resolve("Steam");
        var playStationVisual = ExophasePlatformVisualCatalog.Resolve("PS4");
        var xboxVisual = ExophasePlatformVisualCatalog.Resolve("Xbox Series, Xbox One, Windows");
        var windowsVisual = ExophasePlatformVisualCatalog.Resolve("Windows");
        var switchVisual = ExophasePlatformVisualCatalog.Resolve("Nintendo Switch");
        var osirisVisual = ExophasePlatformVisualCatalog.Resolve("Osiris");
        Expect(steamVisual.DisplayName == "Steam" &&
               playStationVisual.DisplayName == "PlayStation 4" &&
               xboxVisual.DisplayName == "Xbox" &&
               steamVisual.ColorHex == "#66C0F4" &&
               playStationVisual.ColorHex == "#0070D1" &&
               xboxVisual.ColorHex == "#107C10" &&
               windowsVisual.DisplayName == "Windows" &&
               switchVisual.DisplayName == "Nintendo Switch" &&
               switchVisual.ColorHex == "#E60012" &&
               osirisVisual.DisplayName == "Osiris" &&
               osirisVisual.ColorHex == "#F0F1F7" &&
               steamVisual.IconGeometry != null &&
               playStationVisual.IconGeometry != null &&
               xboxVisual.IconGeometry != null &&
               windowsVisual.IconGeometry != null &&
               switchVisual.IconGeometry != null,
            "Known Exophase platforms should resolve to distinct brand colours, names, and vector marks.");
        Expect(windowsVisual.IconGeometry.Bounds.Width == 24d &&
               switchVisual.IconGeometry.Bounds.Width == 24d,
            "Windows and Nintendo Switch should use their proper monochrome brand marks instead of the generic four-square fallback.");

        var controlView = File.ReadAllText(Path.Combine(
            extensionRoot,
            "source",
            "ExophaseActivityControl.xaml"));
        var projectSource = File.ReadAllText(Path.Combine(
            extensionRoot,
            "source",
            "Osiris.Exophase.csproj"));
        var osirisLogoPath = Path.Combine(
            extensionRoot,
            "source",
            "Assets",
            "osiris-logo.png");
        var visualSource = File.ReadAllText(Path.Combine(
            extensionRoot,
            "source",
            "ExophasePlatformVisuals.cs"));
        var activityControlSource = File.ReadAllText(Path.Combine(
            extensionRoot,
            "source",
            "ExophaseActivityControl.xaml.cs"));
        var pluginSource = File.ReadAllText(Path.Combine(extensionRoot, "source", "ExophasePlugin.cs"));
        Expect(controlView.Contains("ItemsSource=\"{Binding Platforms}\"") &&
               controlView.Contains("x:Key=\"PlatformRow\"") &&
               controlView.Contains("Padding\" Value=\"20,15\"") &&
               controlView.Contains("Width\" Value=\"60\"") &&
               controlView.Contains("Height\" Value=\"60\"") &&
               controlView.Contains("BorderBrush\" Value=\"#AFAFAF\"") &&
               controlView.Contains("<ColumnDefinition Width=\"110\" />") &&
               controlView.Contains("Data=\"{Binding IconGeometry}\"") &&
               controlView.Contains("Binding UseOsirisLogo") &&
               controlView.Contains("/Osiris.Exophase;component/Assets/osiris-logo.png") &&
               controlView.Contains("Text=\"{Binding Platform}\"") &&
               controlView.Contains("Text=\"{Binding SourceTag}\"") &&
               controlView.Contains("Foreground=\"#5F6269\"") &&
               controlView.Contains("Text=\"{Binding PlaytimeText}\"") &&
               controlView.Contains("Binding IsLast") &&
               !controlView.Contains("TrophyText") &&
               !controlView.Contains("PlatformBrush") &&
               !controlView.Contains("UniformGrid") &&
               !controlView.Contains("PLAY TIME DISTRIBUTION") &&
               !controlView.Contains("PLATFORM BREAKDOWN") &&
               !controlView.Contains("TOTAL HOURS") &&
               !visualSource.Contains("ExophaseDonutChart") &&
               osirisVisual.UseOsirisLogo &&
               osirisVisual.IconGeometry == null &&
               !steamVisual.UseOsirisLogo &&
               activityControlSource.Contains("UseOsirisLogo = visual.UseOsirisLogo") &&
               activityControlSource.Contains("public bool UseOsirisLogo") &&
               activityControlSource.Contains("public string SourceTag") &&
               activityControlSource.Contains("SourceTag = GetSourceTag(platform)") &&
               activityControlSource.Contains("public bool HasOverflow") &&
               activityControlSource.Contains("HasOverflow = rows.Count > 2") &&
               activityControlSource.Contains("IsCardVisible = rows.Count > 0") &&
               !activityControlSource.Contains("IsCardVisible = resolvedActivity.Enabled") &&
               activityControlSource.Contains("!editableActivity.UsesManualMatch") &&
               projectSource.Contains("Resource Include=\"Assets\\osiris-logo.png\"") &&
               File.Exists(osirisLogoPath) &&
               new FileInfo(osirisLogoPath).Length > 0 &&
               pluginSource.Contains("SourceName = ExtensionSource") &&
               pluginSource.Contains("ActivityControlName = \"ActivityViewControl\"") &&
               pluginSource.Contains("activityResolver") &&
               pluginSource.Contains("gameSettingsStore"),
            "Exophase should clone the HLTB row geometry and use the official Osiris logo for the Osiris platform row.");

        var temporaryRoot = Path.Combine(Path.GetTempPath(), "osiris-exophase-card-" + Guid.NewGuid());
        Directory.CreateDirectory(temporaryRoot);
        try
        {
            var snapshot = new ExophaseActivitySnapshot
            {
                PlayerProfileId = "39458",
                FetchedUtc = new DateTime(2026, 9, 12, 12, 0, 0, DateTimeKind.Utc),
                Games = new List<ExophaseGameActivity>
                {
                    new ExophaseGameActivity
                    {
                        Title = "The Last of Us™ Part I",
                        Platform = "PlayStation 5",
                        PlaytimeSeconds = 34UL * 3600UL,
                        EarnedAwards = 8,
                        TotalAwards = 12
                    },
                    new ExophaseGameActivity
                    {
                        Title = "The Last of Us Part I",
                        Platform = "Steam",
                        PlaytimeSeconds = 19UL * 3600UL,
                        EarnedAwards = 4,
                        TotalAwards = 10
                    },
                    new ExophaseGameActivity
                    {
                        Title = "Only Steam",
                        Platform = "Steam",
                        PlaytimeSeconds = 12UL * 3600UL
                    },
                    new ExophaseGameActivity
                    {
                        Title = "Assassin's Creed IV: Black Flag",
                        Platform = "PlayStation 4",
                        PlaytimeSeconds = 40UL * 3600UL
                    },
                    new ExophaseGameActivity
                    {
                        Title = "The Witcher 3: Wild Hunt - Game of the Year Edition",
                        Platform = "PlayStation 4",
                        PlaytimeSeconds = 70UL * 3600UL,
                        EarnedAwards = 20,
                        TotalAwards = 50
                    },
                    new ExophaseGameActivity
                    {
                        Title = "The Witcher 3: Wild Hunt - Complete Edition",
                        Platform = "Steam",
                        PlaytimeSeconds = 25UL * 3600UL,
                        EarnedAwards = 10,
                        TotalAwards = 30
                    },
                    new ExophaseGameActivity
                    {
                        Title = "The Witcher 3: Wild Hunt - Complete Edition",
                        Platform = "PlayStation 4",
                        PlaytimeSeconds = 5UL * 3600UL,
                        EarnedAwards = 2,
                        TotalAwards = 10
                    }
                }
            };
            File.WriteAllText(
                Path.Combine(temporaryRoot, "exophase-activity-latest.json"),
                JsonConvert.SerializeObject(snapshot));
            var store = new ExophaseActivityStore(temporaryRoot);
            var lookup = store.FindGame("The Last of Us: Part I");
            Expect(lookup.HasSnapshot &&
                   !lookup.SnapshotIsInvalid &&
                   lookup.Game != null &&
                   lookup.Game.Platforms.Count == 2 &&
                   lookup.Game.TotalPlaytimeSeconds == 53UL * 3600UL,
                "The details-card store should read and aggregate the latest private Exophase snapshot.");
            var searchMatches = store.SearchGames("last of us", 10);
            Expect(searchMatches.Count == 1 &&
                   searchMatches[0].MatchKey ==
                       ExophaseActivityParser.NormalizeTitle("The Last of Us Part I") &&
                   searchMatches[0].Platforms.Count == 2 &&
                   store.SearchGames(string.Empty, 10).Count == 0,
                "Game Edit should search the synchronized Exophase library without changing its snapshot.");
            var fuzzyMatches = store.SearchGames("Assassin's Creed Black Flag Resynced", 10);
            Expect(fuzzyMatches.Count == 1 &&
                   fuzzyMatches[0].Title == "Assassin's Creed IV: Black Flag",
                "Game Edit search should tolerate platform, edition, and release-title differences.");

            var changed = false;
            store.SnapshotChanged += () => changed = true;
            store.Update(snapshot);
            Expect(changed,
                "A completed Exophase synchronization should refresh any visible details card.");

            var gameSettings = new ExophaseGameSettingsStore(temporaryRoot);
            var resolver = new ExophaseActivityResolver(store, gameSettings);
            var localGame = new Game
            {
                Id = Guid.NewGuid(),
                Name = "The Last of Us Part I",
                PluginId = Guid.Empty,
                Playtime = 45UL * 3600UL
            };
            var localTotal = resolver.Resolve(localGame, true);
            Expect(localTotal.PositivePlatformCount == 3 &&
                   localTotal.TotalPlaytimeSeconds == 98UL * 3600UL &&
                   localTotal.Platforms.Any(item => item.Platform == "Osiris"),
                "A local Osiris baseline should remain a distinct platform in the cross-platform total.");

            var differentlyNamedGame = new Game
            {
                Id = Guid.NewGuid(),
                Name = "The Last of Us Remake - Local Copy",
                PluginId = Guid.Empty,
                Playtime = 45UL * 3600UL
            };
            gameSettings.Save(differentlyNamedGame.Id, new ExophaseGameSettings
            {
                MatchedTitle = "The Last of Us Part I"
            });
            var manuallyMatchedTotal = resolver.Resolve(differentlyNamedGame, true);
            Expect(manuallyMatchedTotal.UsesManualMatch &&
                   manuallyMatchedTotal.MatchedTitle == "The Last of Us Part I" &&
                   manuallyMatchedTotal.MatchedTitles.Count == 1 &&
                   manuallyMatchedTotal.PositivePlatformCount == 3 &&
                   manuallyMatchedTotal.TotalPlaytimeSeconds == 98UL * 3600UL,
                "A saved Exophase title selection should resolve platform time for a differently named Osiris game.");

            var multiEditionGame = new Game
            {
                Id = Guid.NewGuid(),
                Name = "The Witcher 3",
                PluginId = Guid.Empty,
                Playtime = 8UL * 3600UL
            };
            gameSettings.Save(multiEditionGame.Id, new ExophaseGameSettings
            {
                MatchedTitles = new List<string>
                {
                    "The Witcher 3: Wild Hunt - Game of the Year Edition",
                    "The Witcher 3: Wild Hunt - Complete Edition"
                }
            });
            var multiEditionTotal = resolver.Resolve(multiEditionGame, true);
            Expect(multiEditionTotal.UsesManualMatch &&
                   multiEditionTotal.MatchedTitles.Count == 2 &&
                   multiEditionTotal.PositivePlatformCount == 3 &&
                   multiEditionTotal.TotalPlaytimeSeconds == 108UL * 3600UL &&
                   multiEditionTotal.Platforms.Single(item => item.Platform == "PlayStation 4").PlaytimeSeconds ==
                       75UL * 3600UL &&
                   multiEditionTotal.Platforms.Single(item => item.Platform == "Steam").PlaytimeSeconds ==
                       25UL * 3600UL,
                "Multiple Exophase editions should combine their platform activity without losing the local Osiris baseline.");

            var legacyImportedGame = new Game
            {
                Id = Guid.NewGuid(),
                Name = "The Last of Us Part I",
                PluginId = Guid.Empty,
                Playtime = 53UL * 3600UL
            };
            var legacyImportedTotal = resolver.Resolve(legacyImportedGame, true);
            Expect(legacyImportedTotal.PositivePlatformCount == 2 &&
                   legacyImportedTotal.TotalPlaytimeSeconds == 53UL * 3600UL &&
                   legacyImportedTotal.Platforms.All(item => item.Platform != "Osiris"),
                "A native value matching the old imported aggregate should not be counted again as Osiris time.");

            var steamGame = new Game
            {
                Id = Guid.NewGuid(),
                Name = "The Last of Us Part I",
                PluginId = new Guid("cb91dfc9-b977-43bf-8e70-55f46e410fab"),
                Playtime = 18UL * 3600UL
            };
            gameSettings.Save(steamGame.Id, new ExophaseGameSettings
            {
                Platforms = new List<ExophasePlatformSetting>
                {
                    new ExophasePlatformSetting
                    {
                        Id = "manual:steam",
                        Platform = "Steam",
                        PlaytimeSeconds = 100UL * 3600UL,
                        IsManual = true
                    }
                }
            });
            var steamTotal = resolver.Resolve(steamGame, true);
            Expect(steamTotal.PositivePlatformCount == 2 &&
                   steamTotal.TotalPlaytimeSeconds == 52UL * 3600UL &&
                   steamTotal.CanDisplayTotal &&
                   steamTotal.Platforms.Single(item => item.Platform == "Steam").PlaytimeSeconds == 18UL * 3600UL,
                "The current integrated-library source should use native Time Played and ignore matching Exophase/manual time.");
            var unplayedSteamGame = new Game
            {
                Id = Guid.NewGuid(),
                Name = "The Last of Us Part I",
                PluginId = new Guid("cb91dfc9-b977-43bf-8e70-55f46e410fab"),
                Playtime = 0UL
            };
            var unplayedSteamTotal = resolver.Resolve(unplayedSteamGame, true);
            Expect(unplayedSteamTotal.PositivePlatformCount == 1 &&
                   unplayedSteamTotal.CanDisplayTotal &&
                   unplayedSteamTotal.TotalPlaytimeSeconds == 34UL * 3600UL &&
                   unplayedSteamTotal.Platforms.Single(item => item.Platform == "Steam").PlaytimeSeconds == 0UL,
                "A zero-hour integrated source should still unlock the total when another platform has positive time.");
            var steamOnlyGame = new Game
            {
                Id = Guid.NewGuid(),
                Name = "Only Steam",
                PluginId = new Guid("cb91dfc9-b977-43bf-8e70-55f46e410fab"),
                Playtime = 5UL * 3600UL
            };
            var steamOnly = resolver.Resolve(steamOnlyGame, true);
            Expect(steamOnly.PositivePlatformCount == 1 &&
                   steamOnly.TotalPlaytimeSeconds == 5UL * 3600UL &&
                   !steamOnly.CanDisplayTotal,
                "A game played on only its current library platform should keep native Time Played without an Exophase total.");
            var unplayedLocalGame = new Game
            {
                Id = Guid.NewGuid(),
                Name = "Only Steam",
                PluginId = Guid.Empty,
                Playtime = 0UL
            };
            var unplayedLocalTotal = resolver.Resolve(unplayedLocalGame, true);
            Expect(unplayedLocalTotal.PositivePlatformCount == 1 &&
                   unplayedLocalTotal.CanDisplayTotal &&
                   unplayedLocalTotal.TotalPlaytimeSeconds == 12UL * 3600UL &&
                   unplayedLocalTotal.Platforms.Single(item => item.Platform == "Osiris").PlaytimeSeconds == 0UL,
                "A zero-hour local Osiris source should still unlock the total when an external platform has positive time.");
            Expect(ExophaseActivityResolver.NormalizePlatformKey("Xbox Series X|S") ==
                   ExophaseActivityResolver.NormalizePlatformKey("Xbox"),
                "Platform aliases should resolve to one source bucket before totals are calculated.");

            gameSettings.Save(localGame.Id, new ExophaseGameSettings
            {
                Enabled = false,
                MatchedTitle = "The Last of Us Part I",
                Platforms = new List<ExophasePlatformSetting>
                {
                    new ExophasePlatformSetting
                    {
                        Id = "manual:nintendo",
                        Platform = "Nintendo Switch",
                        PlaytimeSeconds = 2UL * 3600UL,
                        IsManual = true
                    }
                }
            });
            var reloadedSettings = new ExophaseGameSettingsStore(temporaryRoot).Load(localGame.Id);
            Expect(!reloadedSettings.Enabled &&
                   reloadedSettings.MatchedTitle == "The Last of Us Part I" &&
                   reloadedSettings.MatchedTitles.SequenceEqual(new[] { "The Last of Us Part I" }) &&
                   reloadedSettings.Platforms.Count == 1 &&
                   reloadedSettings.Platforms[0].Platform == "Nintendo Switch",
                "Per-game enable state, explicit match, and manual platform time should survive a restart.");
        }
        finally
        {
            Directory.Delete(temporaryRoot, true);
        }
    }

    private static void Expect(bool condition, string message)
    {
        checks++;
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
