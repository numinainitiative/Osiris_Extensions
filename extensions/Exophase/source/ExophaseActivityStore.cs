using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Osiris.Extensions.Exophase
{
    internal sealed class ExophaseActivityLookup
    {
        public bool HasSnapshot { get; set; }

        public bool SnapshotIsInvalid { get; set; }

        public DateTime FetchedUtc { get; set; }

        public ExophaseGameAggregate Game { get; set; }
    }

    internal sealed class ExophaseActivityStore
    {
        private static readonly HashSet<string> SearchNoiseWords = new HashSet<string>(
            new[]
            {
                "a", "an", "and", "bundle", "collection", "complete", "definitive",
                "edition", "game", "goty", "of", "remake", "remastered", "resynced", "the"
            },
            StringComparer.OrdinalIgnoreCase);
        private readonly object syncRoot = new object();
        private readonly string snapshotPath;
        private bool loaded;
        private long loadedLength = -1;
        private DateTime loadedWriteUtc;
        private bool hasSnapshot;
        private bool snapshotIsInvalid;
        private DateTime fetchedUtc;
        private Dictionary<string, ExophaseGameAggregate> gamesByMatchKey =
            new Dictionary<string, ExophaseGameAggregate>(StringComparer.Ordinal);

        public event Action SnapshotChanged;

        public ExophaseActivityStore(string userDataPath)
        {
            if (string.IsNullOrWhiteSpace(userDataPath))
            {
                throw new ArgumentException("An Exophase user-data path is required.", nameof(userDataPath));
            }

            snapshotPath = Path.Combine(userDataPath, "exophase-activity-latest.json");
        }

        public ExophaseActivityLookup FindGame(string title)
        {
            EnsureCurrent();
            lock (syncRoot)
            {
                ExophaseGameAggregate game;
                gamesByMatchKey.TryGetValue(
                    ExophaseActivityParser.NormalizeTitle(title),
                    out game);
                return new ExophaseActivityLookup
                {
                    HasSnapshot = hasSnapshot,
                    SnapshotIsInvalid = snapshotIsInvalid,
                    FetchedUtc = fetchedUtc,
                    Game = Clone(game)
                };
            }
        }

        public List<ExophaseGameAggregate> SearchGames(string query, int maximumResults = 50)
        {
            EnsureCurrent();
            var normalizedQuery = ExophaseActivityParser.NormalizeTitle(query);
            if (string.IsNullOrEmpty(normalizedQuery) || maximumResults <= 0)
            {
                return new List<ExophaseGameAggregate>();
            }

            var queryTokens = GetSearchTokens(query);
            lock (syncRoot)
            {
                return gamesByMatchKey.Values
                    .Where(game => game != null)
                    .Select(game => new
                    {
                        Game = game,
                        Score = GetSearchScore(game, normalizedQuery, queryTokens)
                    })
                    .Where(result => result.Score > 0)
                    .OrderByDescending(result => result.Score)
                    .ThenBy(result => result.Game.Title, StringComparer.OrdinalIgnoreCase)
                    .Take(maximumResults)
                    .Select(result => Clone(result.Game))
                    .ToList();
            }
        }

        private static int GetSearchScore(
            ExophaseGameAggregate game,
            string normalizedQuery,
            HashSet<string> queryTokens)
        {
            if (string.Equals(game.MatchKey, normalizedQuery, StringComparison.Ordinal))
            {
                return 10000;
            }

            if (game.MatchKey.Contains(normalizedQuery))
            {
                return 9000 - Math.Min(1000, game.MatchKey.Length - normalizedQuery.Length);
            }

            if (normalizedQuery.Contains(game.MatchKey))
            {
                return 8000 - Math.Min(1000, normalizedQuery.Length - game.MatchKey.Length);
            }

            var gameTokens = GetSearchTokens(game.Title);
            var overlap = queryTokens.Count(token => gameTokens.Contains(token));
            if (overlap == 0 || (queryTokens.Count > 1 && overlap < 2))
            {
                return 0;
            }

            var queryCoverage = overlap * 100 / Math.Max(1, queryTokens.Count);
            var gameCoverage = overlap * 100 / Math.Max(1, gameTokens.Count);
            return queryCoverage * 10 + gameCoverage;
        }

        private static HashSet<string> GetSearchTokens(string value)
        {
            var separated = new string((value ?? string.Empty)
                .Select(character => char.IsLetterOrDigit(character)
                    ? char.ToLowerInvariant(character)
                    : ' ')
                .ToArray());
            return new HashSet<string>(
                separated.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(token => token.Length > 1 && !SearchNoiseWords.Contains(token)),
                StringComparer.OrdinalIgnoreCase);
        }

        public void Update(ExophaseActivitySnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            lock (syncRoot)
            {
                SetSnapshot(snapshot);
                var info = new FileInfo(snapshotPath);
                loadedLength = info.Exists ? info.Length : -1;
                loadedWriteUtc = info.Exists ? info.LastWriteTimeUtc : DateTime.MinValue;
                loaded = true;
            }

            SnapshotChanged?.Invoke();
        }

        private void EnsureCurrent()
        {
            lock (syncRoot)
            {
                var info = new FileInfo(snapshotPath);
                var length = info.Exists ? info.Length : -1;
                var writeUtc = info.Exists ? info.LastWriteTimeUtc : DateTime.MinValue;
                if (loaded && length == loadedLength && writeUtc == loadedWriteUtc)
                {
                    return;
                }

                loaded = true;
                loadedLength = length;
                loadedWriteUtc = writeUtc;
                hasSnapshot = false;
                snapshotIsInvalid = false;
                fetchedUtc = DateTime.MinValue;
                gamesByMatchKey = new Dictionary<string, ExophaseGameAggregate>(StringComparer.Ordinal);
                if (!info.Exists)
                {
                    return;
                }

                try
                {
                    var snapshot = JsonConvert.DeserializeObject<ExophaseActivitySnapshot>(
                        File.ReadAllText(snapshotPath));
                    if (snapshot?.Games == null || snapshot.Games.Count == 0)
                    {
                        throw new InvalidDataException("The Exophase activity snapshot has no game records.");
                    }

                    SetSnapshot(snapshot);
                }
                catch
                {
                    snapshotIsInvalid = true;
                }
            }
        }

        private void SetSnapshot(ExophaseActivitySnapshot snapshot)
        {
            gamesByMatchKey = ExophaseActivityParser.AggregateGames(snapshot.Games)
                .Where(game => !string.IsNullOrEmpty(game.MatchKey))
                .GroupBy(game => game.MatchKey, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            fetchedUtc = snapshot.FetchedUtc;
            hasSnapshot = true;
            snapshotIsInvalid = false;
        }

        private static ExophaseGameAggregate Clone(ExophaseGameAggregate game)
        {
            if (game == null)
            {
                return null;
            }

            return new ExophaseGameAggregate
            {
                MatchKey = game.MatchKey,
                Title = game.Title,
                TotalPlaytimeSeconds = game.TotalPlaytimeSeconds,
                Platforms = (game.Platforms ?? new List<ExophasePlatformActivity>())
                    .Select(platform => new ExophasePlatformActivity
                    {
                        Platform = platform.Platform,
                        PlaytimeSeconds = platform.PlaytimeSeconds,
                        EarnedAwards = platform.EarnedAwards,
                        TotalAwards = platform.TotalAwards
                    })
                    .ToList()
            };
        }
    }
}
