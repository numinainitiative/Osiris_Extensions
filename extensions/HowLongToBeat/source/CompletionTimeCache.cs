using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Osiris.Extensions.HowLongToBeat
{
    internal sealed class CompletionTimeCache
    {
        private readonly object syncRoot = new object();
        private readonly string cachePath;
        private Dictionary<string, CompletionTimeCacheRecord> records;

        public CompletionTimeCache(string userDataPath)
        {
            Directory.CreateDirectory(userDataPath);
            cachePath = Path.Combine(userDataPath, "completion-times-cache.json");
            records = Load();
        }

        public bool TryGet(string gameName, int? releaseYear, bool requireFresh, out CompletionTimeResult result)
        {
            lock (syncRoot)
            {
                CompletionTimeCacheRecord record;
                if (!records.TryGetValue(BuildKey(gameName, releaseYear), out record) || record?.Result == null)
                {
                    result = null;
                    return false;
                }

                // Successful estimates are durable user data. Once downloaded,
                // they remain available indefinitely and are replaced only by
                // an explicit per-game rematch or another deliberate change.
                // Empty searches retain a short lifetime so a game can be found
                // later if HLTB adds it.
                if (requireFresh &&
                    !record.Result.Found &&
                    DateTime.UtcNow - record.Result.FetchedUtc > TimeSpan.FromDays(1))
                {
                    result = null;
                    return false;
                }

                result = record.Result.Clone();
                return true;
            }
        }

        public void Store(string gameName, int? releaseYear, CompletionTimeResult result)
        {
            if (result == null)
            {
                return;
            }

            lock (syncRoot)
            {
                records[BuildKey(gameName, releaseYear)] = new CompletionTimeCacheRecord
                {
                    GameName = gameName,
                    ReleaseYear = releaseYear,
                    Result = result.Clone()
                };
                Save();
            }
        }

        private Dictionary<string, CompletionTimeCacheRecord> Load()
        {
            try
            {
                if (!File.Exists(cachePath))
                {
                    return new Dictionary<string, CompletionTimeCacheRecord>(StringComparer.OrdinalIgnoreCase);
                }

                var parsed = JsonConvert.DeserializeObject<Dictionary<string, CompletionTimeCacheRecord>>(File.ReadAllText(cachePath));
                return parsed == null
                    ? new Dictionary<string, CompletionTimeCacheRecord>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, CompletionTimeCacheRecord>(parsed, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, CompletionTimeCacheRecord>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private void Save()
        {
            var temporaryPath = cachePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonConvert.SerializeObject(records, Formatting.Indented));

            if (File.Exists(cachePath))
            {
                File.Replace(temporaryPath, cachePath, null);
            }
            else
            {
                File.Move(temporaryPath, cachePath);
            }
        }

        private static string BuildKey(string gameName, int? releaseYear)
        {
            return $"{CompletionTimeMatching.Normalize(gameName)}|{releaseYear?.ToString() ?? string.Empty}";
        }
    }
}
