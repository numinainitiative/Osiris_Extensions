using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Osiris.Extensions.Exophase
{
    internal sealed class ExophaseGameSettings
    {
        public bool Enabled { get; set; } = true;

        public string MatchedTitle { get; set; }

        public List<ExophasePlatformSetting> Platforms { get; set; } =
            new List<ExophasePlatformSetting>();

        public ExophaseGameSettings Clone()
        {
            return new ExophaseGameSettings
            {
                Enabled = Enabled,
                MatchedTitle = MatchedTitle,
                Platforms = (Platforms ?? new List<ExophasePlatformSetting>())
                    .Where(item => item != null)
                    .Select(item => item.Clone())
                    .ToList()
            };
        }
    }

    internal sealed class ExophasePlatformSetting
    {
        public string Id { get; set; }

        public string SourceKey { get; set; }

        public string Platform { get; set; }

        public ulong PlaytimeSeconds { get; set; }

        public bool IsManual { get; set; }

        public ExophasePlatformSetting Clone()
        {
            return (ExophasePlatformSetting)MemberwiseClone();
        }
    }

    internal sealed class ExophaseGameSettingsStore
    {
        private readonly object syncRoot = new object();
        private readonly string settingsPath;
        private Dictionary<string, ExophaseGameSettings> settingsByGame;

        public event Action<Guid> SettingsChanged;

        public ExophaseGameSettingsStore(string userDataPath)
        {
            if (string.IsNullOrWhiteSpace(userDataPath))
            {
                throw new ArgumentException("A private extension data path is required.", nameof(userDataPath));
            }

            Directory.CreateDirectory(userDataPath);
            settingsPath = Path.Combine(userDataPath, "game-settings.json");
            settingsByGame = LoadAll();
        }

        public ExophaseGameSettings Load(Guid gameId)
        {
            lock (syncRoot)
            {
                ExophaseGameSettings value;
                return settingsByGame.TryGetValue(gameId.ToString("N"), out value)
                    ? Sanitize(value).Clone()
                    : new ExophaseGameSettings();
            }
        }

        public void Save(Guid gameId, ExophaseGameSettings value)
        {
            var sanitized = Sanitize(value);
            lock (syncRoot)
            {
                settingsByGame[gameId.ToString("N")] = sanitized.Clone();
                WriteAll();
            }

            SettingsChanged?.Invoke(gameId);
        }

        private Dictionary<string, ExophaseGameSettings> LoadAll()
        {
            if (!File.Exists(settingsPath))
            {
                return new Dictionary<string, ExophaseGameSettings>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                var values = JsonConvert.DeserializeObject<Dictionary<string, ExophaseGameSettings>>(
                    File.ReadAllText(settingsPath));
                return (values ?? new Dictionary<string, ExophaseGameSettings>())
                    .Where(pair => Guid.TryParse(pair.Key, out _))
                    .ToDictionary(
                        pair => pair.Key,
                        pair => Sanitize(pair.Value),
                        StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, ExophaseGameSettings>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private void WriteAll()
        {
            var temporaryPath = settingsPath + ".tmp";
            try
            {
                File.WriteAllText(
                    temporaryPath,
                    JsonConvert.SerializeObject(settingsByGame, Formatting.Indented));
                if (File.Exists(settingsPath))
                {
                    File.Replace(temporaryPath, settingsPath, null, true);
                }
                else
                {
                    File.Move(temporaryPath, settingsPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static ExophaseGameSettings Sanitize(ExophaseGameSettings value)
        {
            var source = value ?? new ExophaseGameSettings();
            var platforms = new List<ExophasePlatformSetting>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in source.Platforms ?? new List<ExophasePlatformSetting>())
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Platform))
                {
                    continue;
                }

                var id = string.IsNullOrWhiteSpace(item.Id)
                    ? "manual:" + Guid.NewGuid().ToString("N")
                    : item.Id.Trim();
                if (!ids.Add(id))
                {
                    continue;
                }

                platforms.Add(new ExophasePlatformSetting
                {
                    Id = id,
                    SourceKey = item.SourceKey?.Trim(),
                    Platform = item.Platform.Trim(),
                    PlaytimeSeconds = item.PlaytimeSeconds,
                    IsManual = item.IsManual
                });
            }

            return new ExophaseGameSettings
            {
                Enabled = source.Enabled,
                MatchedTitle = string.IsNullOrWhiteSpace(source.MatchedTitle)
                    ? null
                    : source.MatchedTitle.Trim(),
                Platforms = platforms
            };
        }
    }
}
