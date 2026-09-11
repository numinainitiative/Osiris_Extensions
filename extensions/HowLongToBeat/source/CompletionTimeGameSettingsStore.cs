using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Osiris.Extensions.HowLongToBeat
{
    internal sealed class CompletionTimeGameSettingsStore
    {
        private readonly object syncRoot = new object();
        private readonly string settingsPath;
        private Dictionary<string, CompletionTimeGameSettings> settingsByGame;

        public event Action<Guid> SettingsChanged;

        public CompletionTimeGameSettingsStore(string userDataPath)
        {
            if (string.IsNullOrWhiteSpace(userDataPath))
            {
                throw new ArgumentException("A private extension data path is required.", nameof(userDataPath));
            }

            Directory.CreateDirectory(userDataPath);
            settingsPath = Path.Combine(userDataPath, "game-settings.json");
            settingsByGame = LoadAll();
        }

        public CompletionTimeGameSettings Load(Guid gameId)
        {
            lock (syncRoot)
            {
                CompletionTimeGameSettings settings;
                return settingsByGame.TryGetValue(gameId.ToString("D"), out settings) && settings != null
                    ? settings.Clone()
                    : new CompletionTimeGameSettings();
            }
        }

        public void Save(Guid gameId, CompletionTimeGameSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            lock (syncRoot)
            {
                settingsByGame[gameId.ToString("D")] = settings.Clone();
                SaveAll();
            }

            SettingsChanged?.Invoke(gameId);
        }

        public void StoreFetchedResult(
            Guid gameId,
            bool isManual,
            string gameName,
            int? releaseYear,
            CompletionTimeResult result,
            bool notify)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            lock (syncRoot)
            {
                CompletionTimeGameSettings settings;
                if (!settingsByGame.TryGetValue(gameId.ToString("D"), out settings) || settings == null)
                {
                    settings = new CompletionTimeGameSettings();
                }
                else
                {
                    settings = settings.Clone();
                }

                if (isManual)
                {
                    settings.ManualResult = result.Clone();
                }
                else
                {
                    settings.AutomaticResult = result.Clone();
                    settings.AutomaticGameName = gameName;
                    settings.AutomaticReleaseYear = releaseYear;
                }

                settingsByGame[gameId.ToString("D")] = settings;
                SaveAll();
            }

            if (notify)
            {
                SettingsChanged?.Invoke(gameId);
            }
        }

        private Dictionary<string, CompletionTimeGameSettings> LoadAll()
        {
            try
            {
                if (!File.Exists(settingsPath))
                {
                    return CreateEmpty();
                }

                var parsed = JsonConvert.DeserializeObject<Dictionary<string, CompletionTimeGameSettings>>(
                    File.ReadAllText(settingsPath));
                return parsed == null
                    ? CreateEmpty()
                    : new Dictionary<string, CompletionTimeGameSettings>(parsed, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return CreateEmpty();
            }
        }

        private void SaveAll()
        {
            var temporaryPath = settingsPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonConvert.SerializeObject(settingsByGame, Formatting.Indented));
            if (File.Exists(settingsPath))
            {
                File.Replace(temporaryPath, settingsPath, null);
            }
            else
            {
                File.Move(temporaryPath, settingsPath);
            }
        }

        private static Dictionary<string, CompletionTimeGameSettings> CreateEmpty()
        {
            return new Dictionary<string, CompletionTimeGameSettings>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
