using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace Osiris.Extensions.Stats
{
    internal sealed class StatsSessionRecord
    {
        public Guid GameId { get; set; }

        public DateTime StartedUtc { get; set; }

        public DateTime EndedUtc { get; set; }

        public ulong Seconds { get; set; }
    }

    internal sealed class StatsSessionLedger
    {
        private const string StatsExtensionFolder =
            "Stats_511681f5-d2f5-41ae-8b73-fa3adcc86c85";
        private const string SessionFileName = "session-history.tsv";

        private static readonly ILogger Logger = LogManager.GetLogger();
        private readonly object gate = new object();
        private readonly string filePath;
        private readonly List<StatsSessionRecord> records = new List<StatsSessionRecord>();

        public event EventHandler Changed;

        public StatsSessionLedger(IPlayniteAPI api)
        {
            filePath = Path.Combine(
                api.Paths.ExtensionsDataPath,
                "Extras",
                StatsExtensionFolder,
                SessionFileName);
            Load();
        }

        public void Record(Game game, ulong elapsedSeconds, DateTime endedUtc)
        {
            if (game == null || game.Hidden || elapsedSeconds == 0UL)
            {
                return;
            }

            var normalizedEnd = endedUtc.Kind == DateTimeKind.Utc
                ? endedUtc
                : endedUtc.ToUniversalTime();
            var boundedSeconds = Math.Min(elapsedSeconds, (ulong)TimeSpan.FromDays(7).TotalSeconds);
            var record = new StatsSessionRecord
            {
                GameId = game.Id,
                EndedUtc = normalizedEnd,
                StartedUtc = normalizedEnd.Subtract(TimeSpan.FromSeconds(boundedSeconds)),
                Seconds = boundedSeconds
            };

            lock (gate)
            {
                records.Add(record);
                Save();
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }

        public IReadOnlyList<StatsSessionRecord> Snapshot(ISet<Guid> visibleGameIds)
        {
            lock (gate)
            {
                return records
                    .Where(record => visibleGameIds == null || visibleGameIds.Contains(record.GameId))
                    .Select(record => new StatsSessionRecord
                    {
                        GameId = record.GameId,
                        StartedUtc = record.StartedUtc,
                        EndedUtc = record.EndedUtc,
                        Seconds = record.Seconds
                    })
                    .ToList();
            }
        }

        private void Load()
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            try
            {
                foreach (var line in File.ReadAllLines(filePath))
                {
                    var parts = line.Split('\t');
                    Guid gameId;
                    DateTime startedUtc;
                    DateTime endedUtc;
                    ulong seconds;
                    if (parts.Length != 4 ||
                        !Guid.TryParse(parts[0], out gameId) ||
                        !DateTime.TryParseExact(
                            parts[1],
                            "o",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind,
                            out startedUtc) ||
                        !DateTime.TryParseExact(
                            parts[2],
                            "o",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind,
                            out endedUtc) ||
                        !ulong.TryParse(
                            parts[3],
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out seconds) ||
                        seconds == 0UL)
                    {
                        continue;
                    }

                    records.Add(new StatsSessionRecord
                    {
                        GameId = gameId,
                        StartedUtc = startedUtc.ToUniversalTime(),
                        EndedUtc = endedUtc.ToUniversalTime(),
                        Seconds = seconds
                    });
                }
            }
            catch (Exception exception)
            {
                Logger.Warn(exception, "Stats session history could not be read.");
            }
        }

        private void Save()
        {
            var temporaryPath = filePath + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                var lines = records
                    .OrderBy(record => record.StartedUtc)
                    .Select(record => string.Join(
                        "\t",
                        record.GameId.ToString("D"),
                        record.StartedUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
                        record.EndedUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
                        record.Seconds.ToString(CultureInfo.InvariantCulture)))
                    .ToArray();
                File.WriteAllLines(temporaryPath, lines);
                if (File.Exists(filePath))
                {
                    File.Replace(temporaryPath, filePath, null);
                }
                else
                {
                    File.Move(temporaryPath, filePath);
                }
            }
            catch (Exception exception)
            {
                Logger.Warn(exception, "Stats session history could not be saved.");
                try
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
                catch
                {
                    // A failed cleanup must not interrupt game shutdown handling.
                }
            }
        }
    }
}
