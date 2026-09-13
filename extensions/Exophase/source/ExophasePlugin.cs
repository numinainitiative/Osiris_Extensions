using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Plugins;

namespace Osiris.Extensions.Exophase
{
    public sealed class ExophasePlugin : GenericPlugin
    {
        private const string ExtensionSource = "Exophase";
        private const string ActivityControlName = "ActivityViewControl";
        private readonly ExophaseSettings settings;
        private readonly ExophaseExtractionService extractionService;
        private readonly ExophaseActivityStore activityStore;
        private readonly ExophaseGameSettingsStore gameSettingsStore;
        private readonly ExophaseActivityResolver activityResolver;

        public override Guid Id { get; } = Guid.Parse("26131977-669a-4ef7-a66c-026122a24089");

        public ExophasePlugin(IPlayniteAPI api) : base(api)
        {
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };

            var userDataPath = GetPluginUserDataPath();
            extractionService = new ExophaseExtractionService(api, userDataPath);
            activityStore = new ExophaseActivityStore(userDataPath);
            gameSettingsStore = new ExophaseGameSettingsStore(userDataPath);
            activityResolver = new ExophaseActivityResolver(activityStore, gameSettingsStore);
            settings = LoadPluginSettings<ExophaseSettings>() ?? new ExophaseSettings();
            settings.Attach(this, new ExophaseAuthenticationService(api));

            AddCustomElementSupport(new AddCustomElementSupportArgs
            {
                SourceName = ExtensionSource,
                ElementList = new List<string> { ActivityControlName }
            });
        }

        public override Control GetGameViewControl(GetGameViewControlArgs args)
        {
            return args.Name == ActivityControlName
                ? new ExophaseActivityControl(
                    PlayniteApi,
                    activityStore,
                    gameSettingsStore,
                    activityResolver,
                    settings)
                : null;
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new ExophaseSettingsView
            {
                DataContext = settings
            };
        }

        internal void SaveSettings(ExophaseSettings value)
        {
            SavePluginSettings(value);
        }

        public string GetGameSettingsForOsiris(string gameId)
        {
            var game = FindGame(gameId);
            var stored = gameSettingsStore.Load(game.Id);
            var resolved = activityResolver.Resolve(game, false);
            var automaticLookup = activityStore.FindGame(game.Name);
            return JsonConvert.SerializeObject(new
            {
                enabled = stored.Enabled,
                matchedTitle = stored.MatchedTitle,
                automaticPlatforms = (automaticLookup.Game?.Platforms ??
                    new List<ExophasePlatformActivity>()).Select(item => new
                {
                    id = "exophase:" + ExophaseActivityResolver.NormalizePlatformKey(item.Platform),
                    sourceKey = ExophaseActivityResolver.NormalizePlatformKey(item.Platform),
                    platform = item.Platform,
                    playtimeSeconds = item.PlaytimeSeconds,
                    originalPlaytimeSeconds = item.PlaytimeSeconds,
                    isManual = false
                }),
                platforms = resolved.Platforms.Select(item => new
                {
                    id = item.Id,
                    sourceKey = item.SourceKey,
                    platform = item.Platform,
                    playtimeSeconds = item.PlaytimeSeconds,
                    originalPlaytimeSeconds = item.OriginalPlaytimeSeconds,
                    isManual = item.IsManual
                })
            });
        }

        public string SearchGamesForOsiris(string query)
        {
            return JsonConvert.SerializeObject(activityStore.SearchGames(query).Select(game => new
            {
                title = game.Title,
                totalPlaytimeSeconds = game.TotalPlaytimeSeconds,
                platforms = (game.Platforms ?? new List<ExophasePlatformActivity>()).Select(item => new
                {
                    id = "exophase:" + ExophaseActivityResolver.NormalizePlatformKey(item.Platform),
                    sourceKey = ExophaseActivityResolver.NormalizePlatformKey(item.Platform),
                    platform = item.Platform,
                    playtimeSeconds = item.PlaytimeSeconds,
                    originalPlaytimeSeconds = item.PlaytimeSeconds,
                    isManual = false
                })
            }));
        }

        public void SaveGameSettingsForOsiris(string gameId, string settingsJson)
        {
            var game = FindGame(gameId);
            var document = string.IsNullOrWhiteSpace(settingsJson)
                ? new JObject()
                : JObject.Parse(settingsJson);
            var matchedTitle = (document.Value<string>("matchedTitle") ?? string.Empty).Trim();
            if (matchedTitle.Length > 256)
            {
                throw new ArgumentException("The selected Exophase title is too long.", nameof(settingsJson));
            }

            if (matchedTitle.Length > 0)
            {
                var match = activityStore.FindGame(matchedTitle).Game;
                if (match == null)
                {
                    throw new ArgumentException(
                        "The selected Exophase game is no longer present in the synchronized activity data.",
                        nameof(settingsJson));
                }

                matchedTitle = match.Title;
            }

            var savedPlatforms = new List<ExophasePlatformSetting>();
            var platformKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var token in (document["platforms"] as JArray ?? new JArray()).OfType<JObject>())
            {
                var platform = (token.Value<string>("platform") ?? string.Empty).Trim();
                if (platform.Length == 0 || platform.Length > 64)
                {
                    throw new ArgumentException("Every Exophase platform needs a name of 64 characters or fewer.", nameof(settingsJson));
                }

                var platformKey = ExophaseActivityResolver.NormalizePlatformKey(platform);
                if (platformKey.Length == 0 || !platformKeys.Add(platformKey))
                {
                    throw new ArgumentException("Each platform can appear only once for a game.", nameof(settingsJson));
                }

                var seconds = token.Value<ulong?>("playtimeSeconds") ?? 0UL;
                var originalSeconds = token.Value<ulong?>("originalPlaytimeSeconds") ?? seconds;
                var isManual = token.Value<bool?>("isManual") ?? false;
                var id = (token.Value<string>("id") ?? string.Empty).Trim();
                if (isManual)
                {
                    savedPlatforms.Add(new ExophasePlatformSetting
                    {
                        Id = id.StartsWith("manual:", StringComparison.OrdinalIgnoreCase)
                            ? id
                            : "manual:" + Guid.NewGuid().ToString("N"),
                        Platform = platform,
                        PlaytimeSeconds = seconds,
                        IsManual = true
                    });
                }
                else if (seconds != originalSeconds)
                {
                    var sourceKey = (token.Value<string>("sourceKey") ?? platformKey).Trim();
                    savedPlatforms.Add(new ExophasePlatformSetting
                    {
                        Id = "exophase:" + sourceKey,
                        SourceKey = sourceKey,
                        Platform = platform,
                        PlaytimeSeconds = seconds,
                        IsManual = false
                    });
                }
            }

            gameSettingsStore.Save(game.Id, new ExophaseGameSettings
            {
                Enabled = document.Value<bool?>("enabled") ?? true,
                MatchedTitle = matchedTitle.Length == 0 ? null : matchedTitle,
                Platforms = savedPlatforms
            });
        }

        private Playnite.SDK.Models.Game FindGame(string gameId)
        {
            Guid parsedGameId;
            if (!Guid.TryParse(gameId, out parsedGameId))
            {
                throw new ArgumentException("The Osiris game identifier is invalid.", nameof(gameId));
            }

            var game = PlayniteApi.Database.Games.FirstOrDefault(item => item.Id == parsedGameId);
            if (game == null)
            {
                throw new ArgumentException("The Osiris game could not be found.", nameof(gameId));
            }

            return game;
        }

        internal async Task<ExophaseImportSummary> SynchronizeAsync(
            ExophaseSettings preferences,
            IProgress<string> progress,
            CancellationToken cancellationToken)
        {
            if (preferences == null)
            {
                throw new ArgumentNullException(nameof(preferences));
            }

            var snapshot = await extractionService.ExtractAsync(
                preferences.ProfileReference,
                preferences.PlayerProfileId,
                progress,
                cancellationToken).ConfigureAwait(true);
            activityStore.Update(snapshot);
            var aggregates = ExophaseActivityParser.AggregateGames(snapshot.Games);
            var libraryGames = PlayniteApi.Database.Games.ToList();
            var libraryByTitle = libraryGames
                .Where(game => !string.IsNullOrWhiteSpace(game.Name))
                .GroupBy(game => ExophaseActivityParser.NormalizeTitle(game.Name), StringComparer.Ordinal)
                .Where(group => !string.IsNullOrEmpty(group.Key))
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
            var summary = new ExophaseImportSummary
            {
                PlayerProfileId = snapshot.PlayerProfileId,
                Records = snapshot.Games.Count,
                Games = aggregates.Count
            };

            for (var index = 0; index < aggregates.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var aggregate = aggregates[index];
                List<Playnite.SDK.Models.Game> candidates;
                if (!libraryByTitle.TryGetValue(aggregate.MatchKey, out candidates))
                {
                    summary.UnmatchedGames++;
                    continue;
                }

                if (candidates.Count != 1)
                {
                    summary.AmbiguousGames++;
                    continue;
                }

                summary.MatchedGames++;
                AddSaturating(summary, aggregate.TotalPlaytimeSeconds);
                summary.UnchangedGames++;

                if (index == aggregates.Count - 1 || index % 25 == 0)
                {
                    progress?.Report(
                        $"Matching Exophase games {index + 1} of {aggregates.Count}...");
                }
            }

            preferences.PlayerProfileId = snapshot.PlayerProfileId;
            SavePluginSettings(preferences);
            return summary;
        }

        public override async void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            if (!settings.SyncOnStartup)
            {
                return;
            }

            try
            {
                var hasKnownProfile =
                    !string.IsNullOrWhiteSpace(settings.ProfileReference) ||
                    !string.IsNullOrWhiteSpace(settings.PlayerProfileId);
                if (!hasKnownProfile)
                {
                    if (!settings.ConnectAccount)
                    {
                        return;
                    }

                    await settings.RefreshConnectionStatusAsync();
                    if (!settings.IsConnected)
                    {
                        return;
                    }
                }

                await settings.SynchronizeAsync(false);
            }
            catch
            {
                // Settings owns the user-facing status. Startup must continue
                // even if Exophase is offline or changes its private contract.
            }
        }

        private static void AddSaturating(
            ExophaseImportSummary summary,
            ulong value)
        {
            summary.ImportedPlaytimeSeconds = ulong.MaxValue - summary.ImportedPlaytimeSeconds < value
                ? ulong.MaxValue
                : summary.ImportedPlaytimeSeconds + value;
        }
    }
}
