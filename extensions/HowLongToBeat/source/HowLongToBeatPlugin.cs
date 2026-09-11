using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Playnite.SDK;
using Playnite.SDK.Plugins;

namespace Osiris.Extensions.HowLongToBeat
{
    public sealed class HowLongToBeatPlugin : GenericPlugin
    {
        private const string ExtensionSource = "HowLongToBeat";
        private const string ControlName = "CompletionTimesViewControl";
        private readonly HowLongToBeatClient client;
        private readonly CompletionTimeCache cache;
        private readonly CompletionTimeGameSettingsStore gameSettingsStore;
        private readonly HowLongToBeatSettings settings;

        public override Guid Id { get; } = Guid.Parse("fba3e63d-d1a1-4b9d-91c6-091a1220377d");

        public HowLongToBeatPlugin(IPlayniteAPI api) : base(api)
        {
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };
            client = new HowLongToBeatClient();
            var userDataPath = GetPluginUserDataPath();
            cache = new CompletionTimeCache(userDataPath);
            gameSettingsStore = new CompletionTimeGameSettingsStore(userDataPath);
            settings = LoadPluginSettings<HowLongToBeatSettings>() ?? new HowLongToBeatSettings();
            settings.Attach(this);

            AddCustomElementSupport(new AddCustomElementSupportArgs
            {
                SourceName = ExtensionSource,
                ElementList = new List<string> { ControlName }
            });
        }

        public override Control GetGameViewControl(GetGameViewControlArgs args)
        {
            return args.Name == ControlName
                ? new CompletionTimesControl(client, cache, gameSettingsStore, settings)
                : null;
        }

        internal async Task<CompletionDatabaseUpdateSummary> UpdateStoredDatabaseAsync(
            IProgress<string> progress,
            CancellationToken cancellationToken)
        {
            var games = PlayniteApi.Database.Games.ToList();
            var matches = new List<StoredCompletionMatch>();
            foreach (var game in games)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var releaseYear = game.ReleaseDate?.Year;
                var gameSettings = gameSettingsStore.Load(game.Id);
                var isManual = gameSettings.ManualResult?.Found == true &&
                               gameSettings.ManualResult.RemoteGameId > 0;
                var storedResult = isManual
                    ? gameSettings.ManualResult.Clone()
                    : gameSettings.AutomaticResult?.Clone();
                var needsEmbedding = !isManual && storedResult == null;

                CompletionTimeResult cachedResult;
                cache.TryGet(game.Name, releaseYear, false, out cachedResult);
                if (isManual)
                {
                    if (cachedResult?.Found == true &&
                        cachedResult.RemoteGameId == storedResult.RemoteGameId &&
                        cachedResult.HasDetailedProfiles)
                    {
                        storedResult = cachedResult;
                    }
                }
                else if (storedResult == null)
                {
                    storedResult = cachedResult;
                }

                if (storedResult?.Found != true || storedResult.RemoteGameId <= 0)
                {
                    continue;
                }

                matches.Add(new StoredCompletionMatch
                {
                    GameId = game.Id,
                    GameName = game.Name,
                    ReleaseYear = releaseYear,
                    IsManual = isManual,
                    NeedsEmbedding = needsEmbedding,
                    StoredResult = storedResult
                });
            }

            var summary = new CompletionDatabaseUpdateSummary
            {
                LibraryGames = games.Count,
                CheckedGames = matches.Count
            };
            var refreshedById = new Dictionary<long, CompletionTimeResult>();
            var failedIds = new HashSet<long>();

            for (var index = 0; index < matches.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var match = matches[index];
                progress?.Report(
                    $"Checking {index + 1} of {matches.Count}: {match.GameName}");

                CompletionTimeResult refreshedResult;
                if (!refreshedById.TryGetValue(match.StoredResult.RemoteGameId, out refreshedResult) &&
                    !failedIds.Contains(match.StoredResult.RemoteGameId))
                {
                    try
                    {
                        refreshedResult = await client.GetDetailsAsync(
                            match.StoredResult.RemoteGameId,
                            cancellationToken).ConfigureAwait(true);
                        if (refreshedResult?.Found == true)
                        {
                            refreshedById[match.StoredResult.RemoteGameId] = refreshedResult;
                        }
                        else
                        {
                            failedIds.Add(match.StoredResult.RemoteGameId);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch
                    {
                        failedIds.Add(match.StoredResult.RemoteGameId);
                    }
                }

                if (refreshedResult?.Found != true)
                {
                    summary.FailedGames++;
                    continue;
                }

                var replacement = refreshedResult.Clone();
                replacement.RemoteGameId = match.StoredResult.RemoteGameId;
                replacement.MatchedName = string.IsNullOrWhiteSpace(match.StoredResult.MatchedName)
                    ? replacement.MatchedName
                    : match.StoredResult.MatchedName;
                if (match.StoredResult.HasSameTimesAs(replacement))
                {
                    if (match.NeedsEmbedding)
                    {
                        try
                        {
                            gameSettingsStore.StoreFetchedResult(
                                match.GameId,
                                false,
                                match.GameName,
                                match.ReleaseYear,
                                replacement,
                                false);
                        }
                        catch
                        {
                            summary.FailedGames++;
                            continue;
                        }
                    }

                    summary.UnchangedGames++;
                    continue;
                }

                try
                {
                    cache.Store(match.GameName, match.ReleaseYear, replacement);
                    gameSettingsStore.StoreFetchedResult(
                        match.GameId,
                        match.IsManual,
                        match.GameName,
                        match.ReleaseYear,
                        replacement,
                        true);
                    summary.UpdatedGames++;
                }
                catch
                {
                    summary.FailedGames++;
                }
            }

            return summary;
        }

        private sealed class StoredCompletionMatch
        {
            public Guid GameId { get; set; }
            public string GameName { get; set; }
            public int? ReleaseYear { get; set; }
            public bool IsManual { get; set; }
            public bool NeedsEmbedding { get; set; }
            public CompletionTimeResult StoredResult { get; set; }
        }

        public string GetGameSettingsForOsiris(string gameId)
        {
            Guid parsedGameId;
            if (!Guid.TryParse(gameId, out parsedGameId))
            {
                throw new ArgumentException("The Osiris game identifier is invalid.", nameof(gameId));
            }

            var value = gameSettingsStore.Load(parsedGameId);
            return JsonConvert.SerializeObject(new
            {
                enabled = value.Enabled,
                manualResult = value.ManualResult == null ? null : new
                {
                    remoteGameId = value.ManualResult.RemoteGameId,
                    matchedName = value.ManualResult.MatchedName,
                    mainStorySeconds = value.ManualResult.MainStorySeconds,
                    mainExtraSeconds = value.ManualResult.MainExtraSeconds,
                    completionistSeconds = value.ManualResult.CompletionistSeconds
                }
            });
        }

        public string SearchGamesForOsiris(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return "[]";
            }

            var candidates = client.SearchCandidatesAsync(query.Trim(), CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            return JsonConvert.SerializeObject(candidates.Select(candidate => new
            {
                remoteGameId = candidate.GameId,
                matchedName = candidate.GameName,
                releaseYear = candidate.ReleaseWorld,
                gameType = candidate.GameType,
                mainStorySeconds = candidate.MainStorySeconds,
                mainExtraSeconds = candidate.MainExtraSeconds,
                completionistSeconds = candidate.CompletionistSeconds
            }));
        }

        public void SaveGameSettingsForOsiris(string gameId, string settingsJson)
        {
            Guid parsedGameId;
            if (!Guid.TryParse(gameId, out parsedGameId))
            {
                throw new ArgumentException("The Osiris game identifier is invalid.", nameof(gameId));
            }

            var document = string.IsNullOrWhiteSpace(settingsJson)
                ? new JObject()
                : JObject.Parse(settingsJson);
            CompletionTimeResult manualResult = null;
            var manual = document["manualResult"] as JObject;
            var remoteGameId = manual?.Value<long?>("remoteGameId") ?? 0;
            var matchedName = manual?.Value<string>("matchedName");
            if (remoteGameId > 0 && !string.IsNullOrWhiteSpace(matchedName))
            {
                manualResult = new CompletionTimeResult
                {
                    Found = true,
                    RemoteGameId = remoteGameId,
                    MatchedName = matchedName.Trim(),
                    MainStorySeconds = Math.Max(0, manual.Value<long?>("mainStorySeconds") ?? 0),
                    MainExtraSeconds = Math.Max(0, manual.Value<long?>("mainExtraSeconds") ?? 0),
                    CompletionistSeconds = Math.Max(0, manual.Value<long?>("completionistSeconds") ?? 0),
                    FetchedUtc = DateTime.UtcNow
                };
                if (!manualResult.HasAnyTime)
                {
                    throw new ArgumentException("The selected HowLongToBeat game has no completion estimates.", nameof(settingsJson));
                }
            }

            var existingSettings = gameSettingsStore.Load(parsedGameId);
            gameSettingsStore.Save(parsedGameId, new CompletionTimeGameSettings
            {
                Enabled = document.Value<bool?>("enabled") ?? true,
                ManualResult = manualResult,
                AutomaticResult = existingSettings.AutomaticResult?.Clone(),
                AutomaticGameName = existingSettings.AutomaticGameName,
                AutomaticReleaseYear = existingSettings.AutomaticReleaseYear
            });
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new HowLongToBeatSettingsView
            {
                DataContext = settings
            };
        }
    }
}
