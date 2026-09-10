using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
            settings = new HowLongToBeatSettings();

            AddCustomElementSupport(new AddCustomElementSupportArgs
            {
                SourceName = ExtensionSource,
                ElementList = new List<string> { ControlName }
            });
        }

        public override Control GetGameViewControl(GetGameViewControlArgs args)
        {
            return args.Name == ControlName
                ? new CompletionTimesControl(client, cache, gameSettingsStore)
                : null;
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

            gameSettingsStore.Save(parsedGameId, new CompletionTimeGameSettings
            {
                Enabled = document.Value<bool?>("enabled") ?? true,
                ManualResult = manualResult
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
