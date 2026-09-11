using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using Osiris.Extensions.HowLongToBeat;

internal static class Program
{
    private static int checks;

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            RunLocalChecks();
            if (args.Any(argument => string.Equals(argument, "--live", StringComparison.OrdinalIgnoreCase)))
            {
                RunLiveCheck();
            }

            Console.WriteLine($"PASS checks={checks}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL " + exception);
            return 1;
        }
    }

    private static void RunLocalChecks()
    {
        Expect(CompletionTimeFormatting.Format(94005) == "26 Hours", "Main-story hours should round correctly.");
        Expect(CompletionTimeFormatting.Format(3599) == "1 Hour", "Sub-hour estimates should remain readable.");
        Expect(CompletionTimeFormatting.Format(0) == "—", "Missing estimates should use an em dash.");
        Expect(Math.Abs(CompletionTimeFormatting.ProgressPercent(25UL * 3600, 50L * 3600) - 50d) < 0.001,
            "Played-time progress should compare local playtime with the selected completion estimate.");
        Expect(CompletionTimeFormatting.ProgressPercent(75UL * 3600, 50L * 3600) == 100d,
            "Played-time progress should clamp completed estimates to a full bar.");
        Expect(CompletionTimeFormatting.ProgressPercent(25UL * 3600, 0) == 0d,
            "Missing completion estimates should leave the progress track empty.");
        var extensionRoot = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", ".."));
        var cardView = File.ReadAllText(Path.Combine(extensionRoot, "source", "CompletionTimesControl.xaml"));
        Expect(cardView.Contains("<Setter Property=\"CornerRadius\" Value=\"0\" />") &&
               cardView.Contains("<Setter Property=\"Height\" Value=\"10\" />") &&
               cardView.Contains("<ColumnDefinition Width=\"170\" />") &&
               cardView.Contains("<Setter Property=\"Background\" Value=\"Transparent\" />") &&
               cardView.Contains("Geometry=\"M0,10 L4,10 10,0 6,0 Z\"") &&
               cardView.Contains("RenderOptions.EdgeMode=\"Aliased\"") &&
               cardView.Contains("<Setter Property=\"FontSize\" Value=\"18\" />") &&
               cardView.Contains("<Setter Property=\"FontSize\" Value=\"15\" />") &&
               cardView.Contains("Background=\"#F4F5F8\"") &&
               cardView.Contains("CornerRadius=\"5,0,0,5\"") &&
               cardView.Contains("<Trigger Property=\"Value\" Value=\"100\">") &&
               cardView.Contains("TargetName=\"PART_Indicator\"") &&
               cardView.Contains("BorderBrush=\"#626262\"") &&
               !cardView.Contains("#101114") &&
               !cardView.Contains("#3F4249") &&
               !cardView.Contains("#454850"),
            "The card should use transparent icon frames and a compact neutral-grey striped track with a flat partial-fill edge and sealed full state.");
        var bookIcon = File.ReadAllText(Path.Combine(extensionRoot, "source", "Icons", "book-open-check.svg"));
        var pickaxeIcon = File.ReadAllText(Path.Combine(extensionRoot, "source", "Icons", "pickaxe.svg"));
        var trophyIcon = File.ReadAllText(Path.Combine(extensionRoot, "source", "Icons", "trophy.svg"));
        Expect(bookIcon.Contains("lucide lucide-book-open-check") &&
               pickaxeIcon.Contains("lucide lucide-pickaxe") &&
               trophyIcon.Contains("lucide lucide-trophy") &&
               cardView.Contains("Icons/book-open-check.svg") &&
               cardView.Contains("Icons/pickaxe.svg") &&
               cardView.Contains("Icons/trophy.svg"),
            "The card should retain and render geometry from the three supplied original SVG icons.");
        Expect(CompletionTimeMatching.Normalize("Pokémon: Édition!") == "pokemon edition", "Title normalization should remove marks and punctuation.");

        var candidates = new List<SearchCandidate>
        {
            new SearchCandidate
            {
                GameId = 2,
                GameName = "Cyberpunk 2077: Phantom Liberty",
                GameType = "dlc",
                ReleaseWorld = 2023,
                MainStorySeconds = 81000
            },
            new SearchCandidate
            {
                GameId = 1,
                GameName = "Cyberpunk 2077",
                GameType = "game",
                ReleaseWorld = 2020,
                MainStorySeconds = 94005,
                MainExtraSeconds = 227642,
                CompletionistSeconds = 392193
            }
        };
        var selected = CompletionTimeMatching.SelectBest("Cyberpunk 2077", 2020, candidates);
        Expect(selected?.GameId == 1, "Exact base-game result should beat similarly named DLC.");

        var payload = HowLongToBeatClient.BuildPayload("Cyberpunk 2077", "proof", "value");
        Expect(payload.Value<string>("searchType") == "games", "Search payload should target games.");
        Expect(payload.Value<string>("proof") == "value", "Search payload should contain the current proof value.");
        Expect(payload["searchTerms"].Count() == 2, "Search title should be tokenized.");

        var settings = new HowLongToBeatSettings();
        Expect(settings.TimeProfile == CompletionTimeProfiles.Average,
            "Average should be the default completion-time profile.");
        Expect(settings.ShowMainStory && settings.ShowMainExtras && settings.ShowCompletionist,
            "All three completion estimates should be visible by default.");
        Expect(settings.CanUpdateDatabase,
            "The database update action should be available while idle.");
        settings.BeginEdit();
        settings.TimeProfile = CompletionTimeProfiles.Leisure;
        settings.ShowMainExtras = false;
        List<string> settingsErrors;
        Expect(settings.VerifySettings(out settingsErrors), "Completion-time display settings should validate.");
        Expect(settingsErrors != null && settingsErrors.Count == 0, "Valid completion-time settings should not report errors.");
        settings.CancelEdit();
        Expect(settings.TimeProfile == CompletionTimeProfiles.Average && settings.ShowMainExtras,
            "Cancelling settings should restore the original profile and visible fields.");
        settings.EndEdit();

        var settingsView = new HowLongToBeatSettingsView();
        var settingsTabs = settingsView.Content as TabControl;
        var general = settingsTabs?.Items.Cast<TabItem>().FirstOrDefault();
        var dangerZone = settingsTabs?.Items.Cast<TabItem>().LastOrDefault();
        Expect(settingsTabs != null && settingsTabs.Items.Count == 2,
            "HowLongToBeat should expose General and Danger Zone settings categories.");
        Expect(string.Equals(general?.Header as string, "General", StringComparison.Ordinal),
            "General should be the first HowLongToBeat settings category.");
        Expect(string.Equals(dangerZone?.Header as string, "Danger Zone", StringComparison.Ordinal),
            "Danger Zone should remain the last HowLongToBeat settings category.");
        Expect(settingsView.FindName("UpdateDatabaseButton") is Button,
            "General settings should expose the Update Database action.");
        Expect(settingsView.FindName("DatabaseUpdateStatus") is TextBlock,
            "General settings should expose database update progress and results.");

        const string detailPage = "<html><script id=\"__NEXT_DATA__\" type=\"application/json\">" +
            "{\"props\":{\"pageProps\":{\"game\":{\"data\":{\"game\":[{" +
            "\"game_id\":2127,\"game_name\":\"Cyberpunk 2077\"," +
            "\"comp_main\":94005,\"comp_plus\":227642,\"comp_100\":392193," +
            "\"comp_main_l\":65003,\"comp_main_avg\":98009,\"comp_main_med\":90000,\"comp_main_h\":156803," +
            "\"comp_plus_l\":136512,\"comp_plus_avg\":239248,\"comp_plus_med\":216000,\"comp_plus_h\":632212," +
            "\"comp_100_l\":278927,\"comp_100_avg\":413585,\"comp_100_med\":370800,\"comp_100_h\":1108697" +
            "}]}}}}}</script></html>";
        var detailed = HowLongToBeatClient.ParseDetailPage(detailPage, 2127);
        Expect(detailed.GetMainStorySeconds(CompletionTimeProfiles.Rushed) == 65003,
            "Rushed should use the low completion-time estimate.");
        Expect(detailed.GetMainExtraSeconds(CompletionTimeProfiles.Average) == 239248,
            "Average should use the mean completion-time estimate.");
        Expect(detailed.GetCompletionistSeconds(CompletionTimeProfiles.Median) == 370800,
            "Median should use the median completion-time estimate.");
        Expect(detailed.GetCompletionistSeconds(CompletionTimeProfiles.Leisure) == 1108697,
            "Leisure should use the high completion-time estimate.");
        var unchangedDetailed = detailed.Clone();
        Expect(detailed.HasSameTimesAs(unchangedDetailed),
            "Identical exact-match records should not be rewritten.");
        unchangedDetailed.MainStoryAverageSeconds++;
        Expect(!detailed.HasSameTimesAs(unchangedDetailed),
            "A changed completion estimate should be detected for database updating.");

        var temporarySettingsPath = Path.Combine(
            Path.GetTempPath(),
            "Osiris-HLTB-validation-" + Guid.NewGuid().ToString("N"));
        try
        {
            var gameId = Guid.NewGuid();
            var store = new CompletionTimeGameSettingsStore(temporarySettingsPath);
            var defaults = store.Load(gameId);
            Expect(defaults.Enabled && defaults.ManualResult == null,
                "New games should use automatic matching with HowLongToBeat enabled.");

            var cache = new CompletionTimeCache(temporarySettingsPath);
            cache.Store("Durable Game", 2020, new CompletionTimeResult
            {
                Found = true,
                RemoteGameId = 20,
                MatchedName = "Durable Game",
                MainStorySeconds = 36000,
                FetchedUtc = DateTime.UtcNow.AddYears(-10)
            });
            CompletionTimeResult durableResult;
            Expect(cache.TryGet("Durable Game", 2020, true, out durableResult) &&
                   durableResult?.MainStorySeconds == 36000,
                "Successful completion estimates should remain available without expiry.");

            cache.Store("Missing Game", 2020, new CompletionTimeResult
            {
                Found = false,
                FetchedUtc = DateTime.UtcNow.AddDays(-2)
            });
            CompletionTimeResult expiredMiss;
            Expect(!cache.TryGet("Missing Game", 2020, true, out expiredMiss),
                "Unsuccessful searches should still be retried after one day.");

            store.StoreFetchedResult(gameId, false, "Durable Game", 2020, durableResult, false);
            var embedded = new CompletionTimeGameSettingsStore(temporarySettingsPath).Load(gameId);
            Expect(embedded.AutomaticResult?.RemoteGameId == 20 &&
                   embedded.AutomaticResult.MainStorySeconds == 36000 &&
                   embedded.AutomaticGameName == "Durable Game" &&
                   embedded.AutomaticReleaseYear == 2020,
                "Automatic estimates should be embedded in the per-game extension record.");

            store.Save(gameId, new CompletionTimeGameSettings
            {
                Enabled = false,
                AutomaticResult = embedded.AutomaticResult,
                AutomaticGameName = embedded.AutomaticGameName,
                AutomaticReleaseYear = embedded.AutomaticReleaseYear,
                ManualResult = new CompletionTimeResult
                {
                    Found = true,
                    RemoteGameId = 1,
                    MatchedName = "Cyberpunk 2077",
                    MainStorySeconds = 94005,
                    MainExtraSeconds = 227642,
                    CompletionistSeconds = 392193,
                    FetchedUtc = DateTime.UtcNow
                }
            });
            var reloaded = new CompletionTimeGameSettingsStore(temporarySettingsPath).Load(gameId);
            Expect(!reloaded.Enabled && reloaded.ManualResult?.RemoteGameId == 1 &&
                   reloaded.AutomaticResult?.RemoteGameId == 20,
                "Per-game enable state and both exact matches should survive a restart.");
        }
        finally
        {
            if (Directory.Exists(temporarySettingsPath))
            {
                Directory.Delete(temporarySettingsPath, true);
            }
        }
    }

    private static void RunLiveCheck()
    {
        using (var client = new HowLongToBeatClient())
        {
            var result = client.SearchAsync("Cyberpunk 2077", 2020, CancellationToken.None).GetAwaiter().GetResult();
            Expect(result.Found, "Live search should find Cyberpunk 2077.");
            Expect(result.MainStorySeconds > 0, "Live search should return Main Story time.");
            Expect(result.MainExtraSeconds > 0, "Live search should return Main + Extras time.");
            Expect(result.CompletionistSeconds > 0, "Live search should return Completionist time.");
            Expect(result.HasDetailedProfiles, "Live search should return all detailed completion-time profiles.");
            Expect(result.HasProfile(CompletionTimeProfiles.Rushed), "Live search should return Rushed estimates.");
            Expect(result.HasProfile(CompletionTimeProfiles.Average), "Live search should return Average estimates.");
            Expect(result.HasProfile(CompletionTimeProfiles.Median), "Live search should return Median estimates.");
            Expect(result.HasProfile(CompletionTimeProfiles.Leisure), "Live search should return Leisure estimates.");
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
