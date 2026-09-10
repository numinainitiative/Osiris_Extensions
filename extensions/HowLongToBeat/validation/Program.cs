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
        settings.BeginEdit();
        List<string> settingsErrors;
        Expect(settings.VerifySettings(out settingsErrors), "Management-only settings should always validate.");
        Expect(settingsErrors != null && settingsErrors.Count == 0, "Management-only settings should not report errors.");
        settings.CancelEdit();
        settings.EndEdit();

        var settingsView = new HowLongToBeatSettingsView();
        var settingsTabs = settingsView.Content as TabControl;
        var dangerZone = settingsTabs?.Items.Cast<TabItem>().SingleOrDefault();
        Expect(settingsTabs != null && settingsTabs.Items.Count == 1,
            "HowLongToBeat should expose only one settings category for now.");
        Expect(string.Equals(dangerZone?.Header as string, "Danger Zone", StringComparison.Ordinal),
            "The only HowLongToBeat settings category should be Danger Zone.");

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

            store.Save(gameId, new CompletionTimeGameSettings
            {
                Enabled = false,
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
            Expect(!reloaded.Enabled && reloaded.ManualResult?.RemoteGameId == 1,
                "Per-game enable state and manual matches should survive a restart.");
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
