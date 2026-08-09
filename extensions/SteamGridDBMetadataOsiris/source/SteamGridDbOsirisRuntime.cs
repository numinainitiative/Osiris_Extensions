using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;

namespace Osiris.Extensions.SteamGridDBMetadata
{
    internal sealed class SteamGridDbOsirisRuntime
    {
        private const int DisplayPageSize = 20;
        private readonly Func<SteamGridDbSettings> getSettings;

        public SteamGridDbOsirisRuntime(Func<SteamGridDbSettings> getSettings)
        {
            this.getSettings = getSettings ?? throw new ArgumentNullException(nameof(getSettings));
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(getSettings().ApiKey);

        public string SearchGamesJson(string query)
        {
            var settings = getSettings();
            var games = CreateClient(settings).SearchGames(query, CancellationToken.None);
            return JsonConvert.SerializeObject(games);
        }

        public string GetArtworkPageJson(
            string artworkKind,
            string gameIds,
            string assetType,
            int requestedPage,
            string dimensions)
        {
            var settings = getSettings();
            var client = CreateClient(settings);
            var ids = ParseIds(gameIds);
            var dimensionList = ParseValues(dimensions);
            var remainingSkip = Math.Max(0, requestedPage - 1) * DisplayPageSize;
            var result = new List<SteamGridDbImage>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var hasNextPage = false;

            for (var gameIndex = 0; gameIndex < ids.Count; gameIndex++)
            {
                var target = new SteamGridDbTarget { Kind = "game", Id = ids[gameIndex] };
                var first = client.GetImagePage(
                    artworkKind,
                    target,
                    settings.AllowAdultContentArtwork,
                    settings.AllowHumorousArtwork,
                    dimensionList,
                    assetType,
                    0,
                    CancellationToken.None);
                var total = first.Total;
                var limit = Math.Max(1, first.Limit);
                if (remainingSkip >= total)
                {
                    remainingSkip -= total;
                    continue;
                }

                var apiPage = remainingSkip / limit;
                var offset = remainingSkip % limit;
                remainingSkip = 0;
                while ((apiPage * limit) < total)
                {
                    var page = apiPage == 0
                        ? first
                        : client.GetImagePage(
                            artworkKind,
                            target,
                            settings.AllowAdultContentArtwork,
                            settings.AllowHumorousArtwork,
                            dimensionList,
                            assetType,
                            apiPage,
                            CancellationToken.None);
                    if (page.Items.Count == 0) break;
                    for (var itemIndex = offset; itemIndex < page.Items.Count; itemIndex++)
                    {
                        var item = page.Items[itemIndex];
                        if (item == null || string.IsNullOrWhiteSpace(item.Url) || !seen.Add(item.Url)) continue;
                        result.Add(item);
                        if (result.Count == DisplayPageSize)
                        {
                            var consumed = (apiPage * limit) + itemIndex + 1;
                            hasNextPage = consumed < total || gameIndex < ids.Count - 1;
                            return JsonConvert.SerializeObject(new { items = result, hasNextPage });
                        }
                    }
                    offset = 0;
                    apiPage++;
                }
            }

            return JsonConvert.SerializeObject(new { items = result, hasNextPage });
        }

        public string GetPreferredArtworkUrl(
            string artworkKind,
            string gameIds,
            string dimensions,
            bool useAlternateForSimilarRole)
        {
            var settings = getSettings();
            var client = CreateClient(settings);
            var candidates = new List<SteamGridDbImage>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in ParseIds(gameIds))
            {
                var page = client.GetImagePage(
                    artworkKind,
                    new SteamGridDbTarget { Kind = "game", Id = id },
                    settings.AllowAdultContentArtwork,
                    settings.AllowHumorousArtwork,
                    ParseValues(dimensions),
                    "static",
                    0,
                    CancellationToken.None);
                foreach (var item in page.Items)
                {
                    if (item != null && !string.IsNullOrWhiteSpace(item.Url) && seen.Add(item.Url))
                        candidates.Add(item);
                }
            }

            var ranked = candidates
                .OrderByDescending(item => item.Score)
                .ThenByDescending(item => item.Upvotes - item.Downvotes)
                .ThenByDescending(item => item.Id)
                .ToList();
            if (ranked.Count == 0) return null;
            var useAlternate = useAlternateForSimilarRole && !string.Equals(
                settings.SimilarMediaApplication,
                "Same",
                StringComparison.OrdinalIgnoreCase);
            return ranked[Math.Min(useAlternate ? 1 : 0, ranked.Count - 1)].Url;
        }

        private static SteamGridDbApiClient CreateClient(SteamGridDbSettings settings)
        {
            return new SteamGridDbApiClient(settings?.ApiKey);
        }

        private static List<long> ParseIds(string values)
        {
            var result = new List<long>();
            foreach (var value in ParseValues(values))
            {
                long parsed;
                if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) &&
                    parsed > 0 && !result.Contains(parsed))
                    result.Add(parsed);
            }
            return result;
        }

        private static string[] ParseValues(string values)
        {
            return string.IsNullOrWhiteSpace(values)
                ? Array.Empty<string>()
                : values.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => value.Trim())
                    .Where(value => value.Length > 0)
                    .ToArray();
        }
    }
}
