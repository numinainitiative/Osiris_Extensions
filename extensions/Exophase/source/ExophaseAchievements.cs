using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Playnite.SDK;

namespace Osiris.Extensions.Exophase
{
    public sealed partial class ExophasePlugin
    {
        // Versioned, read-only bridge: Exophase owns profile, edition and website access.
        public async Task<string> GetUnlockedAchievementsForOsiris(Guid gameId, CancellationToken token)
        {
            var game = FindGame(gameId.ToString("D"));
            var folder = GetPluginUserDataPath();
            var snapshotPath = Path.Combine(folder, "exophase-activity-latest.json");
            var rawPath = Path.Combine(folder, "exophase-raw-latest.json");
            var snapshot = ReadAchievementSnapshot(snapshotPath);
            var raw = ReadAchievementSnapshot(rawPath);
            var gameSettings = GetGameSettingsForOsiris(gameId.ToString("D"));
            var matches = ExophaseAutoMatch.ResolveAll(game, JObject.Parse(gameSettings), snapshot, raw);
            var pages = new List<ExophasePage>();
            var warnings = new List<string>();
            using (var browser = PlayniteApi.WebViews.CreateOffscreenView())
            {
                foreach (var match in matches)
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        browser.NavigateAndWait(match.Url + "#" + match.AwardPlayerId);
                        var page = ExophasePageParser.Parse(await browser.GetPageSourceAsync());
                        if (page.GameId != match.RemoteId || ExophasePageParser.Key(page.GameTitle) != ExophasePageParser.Key(match.Title))
                            throw new InvalidDataException("Exophase game identity changed.");
                        var earned = await extractionService.DownloadJsonAsync(browser,
                            "https://api.exophase.com/public/player/" + match.AwardPlayerId + "/game/" + match.RemoteId + "/earned?last=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), token);
                        ExophaseAchievementResponse.Apply(page, earned);
                        page.PlayerId = match.ProfileId;
                        if (!string.IsNullOrWhiteSpace(match.Platform)) page.Platform = match.Platform;
                        pages.Add(page);
                        LogManager.GetLogger().Info("Exophase trophies: verified " + page.Awards.Count(a => a.UnlockedUtc.HasValue) + "/" + page.Awards.Count + " earned on " + page.Platform + ".");
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception error) when (error is InvalidDataException || error is InvalidOperationException || error is System.Net.Http.HttpRequestException || error is JsonException)
                    {
                        LogManager.GetLogger().Warn("Exophase trophies: platform fetch failed (" + error.GetType().Name + "). " + (error is InvalidDataException ? error.Message : "Website request unavailable."));
                        // Never substitute completion totals for individual earned records.
                        warnings.Add("One Exophase platform could not be verified; its existing unlocks were kept.");
                    }
                }
            }
            token.ThrowIfCancellationRequested();
            var current = ExophaseAutoMatch.ResolveAll(FindGame(gameId.ToString("D")),
                JObject.Parse(GetGameSettingsForOsiris(gameId.ToString("D"))),
                ReadAchievementSnapshot(snapshotPath), ReadAchievementSnapshot(rawPath));
            if (!matches.Select(Fingerprint).SequenceEqual(current.Select(Fingerprint)))
                throw new InvalidDataException("The linked Exophase edition or profile changed. Sync again.");
            if (pages.Count == 0)
                throw new InvalidDataException("Exophase could not verify individual unlocks. Refresh your profile or complete verification in Exophase, then Sync again. Existing trophies are unchanged.");
            return JsonConvert.SerializeObject(new { SchemaVersion = 1, ProfileId = matches[0].ProfileId, Pages = pages, Warnings = warnings.Distinct().ToArray() });
        }

        private static string Fingerprint(ExophaseAutoMatch match) => match.ProfileId + "|" + match.AwardPlayerId + "|" + match.RemoteId + "|" + match.Url + "|" + match.Title + "|" + match.Platform;

        private static JObject ReadAchievementSnapshot(string path)
        {
            if (!File.Exists(path)) throw new InvalidDataException("Synchronise your profile in the Exophase extension first.");
            if (new FileInfo(path).Length > 25 * 1024 * 1024) throw new InvalidDataException("Exophase snapshot is too large.");
            return JObject.Parse(File.ReadAllText(path));
        }
    }

    public static class ExophaseAchievementResponse
    {
        public static void Apply(ExophasePage page, JObject document)
        {
            if ((bool?)document?["success"] != true || !(document["list"] is JArray list))
                throw new InvalidDataException("Exophase did not return a verified earned-achievement list (success=" + document?["success"] + ", fields=" + string.Join(",", document?.Properties().Select(p => p.Name) ?? Enumerable.Empty<string>()) + ").");
            var values = new Dictionary<string, DateTime>();
            foreach (var entry in list)
            {
                var id = (string)entry["awardid"];
                var date = ExophasePageParser.Timestamp((string)entry["timestamp"]);
                if (string.IsNullOrEmpty(id) || !date.HasValue || values.ContainsKey(id) || !page.Awards.Any(a => a.Id == id))
                    throw new InvalidDataException("Invalid or mismatched Exophase earned achievement.");
                values.Add(id, date.Value);
            }
            foreach (var award in page.Awards) award.UnlockedUtc = values.TryGetValue(award.Id, out var date) ? date : (DateTime?)null;
        }
    }
}
