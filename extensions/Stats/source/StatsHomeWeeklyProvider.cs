using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;

namespace Osiris.Extensions.Stats
{
    internal sealed class StatsHomeWeeklyProvider
    {

        private static readonly ILogger Logger = LogManager.GetLogger();
        private readonly IPlayniteAPI api;
        private readonly StatsSessionLedger sessionLedger;

        internal StatsHomeWeeklyProvider(IPlayniteAPI api, StatsSessionLedger sessionLedger)
        {
            this.api = api;
            this.sessionLedger = sessionLedger;
        }

        internal string GetSnapshotJson()
        {
            var snapshot = BuildSnapshot(DateTime.Now);
            return Serialization.ToJson(snapshot);
        }

        internal StatsHomeWeeklySnapshot BuildSnapshot(DateTime now)
        {
            var games = (api?.Database?.Games ?? Enumerable.Empty<Game>())
                .Where(game => game != null && !game.Hidden)
                .ToList();
            var gameIds = new HashSet<Guid>(games.Select(game => game.Id));
            var monday = now.Date.AddDays(-((int)now.DayOfWeek + 6) % 7);
            var sessions = sessionLedger.Snapshot(gameIds)
                .Where(session =>
                {
                    var ended = session.EndedUtc.ToLocalTime();
                    return ended.Date >= monday && ended <= now;
                })
                .ToList();
            var entries = ReadHomePlaytimeEntries();
            var dailySeconds = new ulong[7];
            foreach (var entry in entries.Where(entry =>
                         gameIds.Contains(entry.GameId) &&
                         entry.Day >= monday &&
                         entry.Day < monday.AddDays(7)))
            {
                var index = (entry.Day - monday).Days;
                dailySeconds[index] = SaturatingAdd(dailySeconds[index], entry.Seconds);
            }

            if (dailySeconds.All(value => value == 0UL))
            {
                foreach (var session in sessions)
                {
                    var index = (session.EndedUtc.ToLocalTime().Date - monday).Days;
                    if (index >= 0 && index < dailySeconds.Length)
                    {
                        dailySeconds[index] = SaturatingAdd(dailySeconds[index], session.Seconds);
                    }
                }
            }

            var weeklySecondsByGame = entries
                .Where(entry =>
                    gameIds.Contains(entry.GameId) &&
                    entry.Day >= monday &&
                    entry.Day < monday.AddDays(7))
                .GroupBy(entry => entry.GameId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Aggregate(
                        0UL,
                        (sum, entry) => SaturatingAdd(sum, entry.Seconds)));
            if (weeklySecondsByGame.Count == 0)
            {
                weeklySecondsByGame = sessions
                    .GroupBy(session => session.GameId)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Aggregate(
                            0UL,
                            (sum, session) => SaturatingAdd(sum, session.Seconds)));
            }

            var totalSeconds = dailySeconds.Aggregate(0UL, SaturatingAdd);
            var averageSeconds = sessions.Count == 0
                ? 0UL
                : sessions.Aggregate(0UL, (sum, session) => SaturatingAdd(sum, session.Seconds)) /
                  (ulong)sessions.Count;
            var favoriteHour = sessions.Count > 0
                ? GetFavoriteHour(sessions.Select(session => session.StartedUtc.ToLocalTime().Hour))
                : GetFavoriteHour(games
                    .Where(game =>
                        game.LastActivity.HasValue &&
                        game.LastActivity.Value.ToLocalTime().Date >= monday &&
                        game.LastActivity.Value.ToLocalTime() <= now)
                    .Select(game => game.LastActivity.Value.ToLocalTime().Hour));
            var gameById = games.ToDictionary(game => game.Id);
            var mostPlayed = weeklySecondsByGame
                .Where(pair => pair.Value > 0UL && gameById.ContainsKey(pair.Key))
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => GetGameName(gameById[pair.Key]), StringComparer.CurrentCultureIgnoreCase)
                .FirstOrDefault();
            var trophyStats = StatsTrophiesSnapshot.Read(api, games);

            return new StatsHomeWeeklySnapshot
            {
                SchemaVersion = 1,
                Metrics = new List<StatsHomeWeeklyMetric>
                {
                    new StatsHomeWeeklyMetric
                    {
                        Key = "TimePlayed",
                        Label = "TIME PLAYED",
                        Value = FormatWeeklyHours(totalSeconds),
                        Bars = Enumerable.Range(0, 7).Select(index => new StatsHomeWeeklyBar
                        {
                            Day = monday.AddDays(index).ToString("ddd", CultureInfo.CurrentCulture).Substring(0, 1),
                            Duration = monday.AddDays(index).ToString("dddd", CultureInfo.CurrentCulture) +
                                ": " + FormatPlaytime(dailySeconds[index]),
                            Seconds = dailySeconds[index],
                            IsToday = monday.AddDays(index).Date == now.Date
                        }).ToList()
                    },
                    new StatsHomeWeeklyMetric
                    {
                        Key = "FavoriteTime",
                        Label = "FAVOURITE TIME TO PLAY",
                        Value = FormatFavoriteHour(favoriteHour)
                    },
                    new StatsHomeWeeklyMetric
                    {
                        Key = "AverageSession",
                        Label = "AVERAGE SESSION",
                        Value = sessions.Count == 0 ? "—" : FormatPlaytime(averageSeconds)
                    },
                    new StatsHomeWeeklyMetric
                    {
                        Key = "TrophiesEarned",
                        Label = "LAST TROPHIES EARNED",
                        Available = trophyStats.Available,
                        Trophies = trophyStats.Recent
                    },
                    new StatsHomeWeeklyMetric
                    {
                        Key = "MostPlayedGame",
                        GameId = mostPlayed.Value == 0UL ? Guid.Empty : mostPlayed.Key,
                        Label = "MOST PLAYED GAME",
                        Value = mostPlayed.Value == 0UL ? "—" : GetGameName(gameById[mostPlayed.Key]),
                        Detail = mostPlayed.Value == 0UL ? null : FormatPlaytime(mostPlayed.Value)
                    }
                }
            };
        }

        private IReadOnlyList<HomePlaytimeEntry> ReadHomePlaytimeEntries()
        {
            try
            {
                var path = Path.Combine(
                    api.Paths.ConfigurationPath,
                    "Settings",
                    "Osiris",
                    "osiris-home-playtime.json");
                var persisted = File.Exists(path)
                    ? Serialization.FromJsonFile<List<HomePlaytimePersistence>>(path)
                    : null;
                return (persisted ?? new List<HomePlaytimePersistence>())
                    .Select(entry => entry?.ToEntry())
                    .Where(entry => entry != null)
                    .ToList();
            }
            catch (Exception exception)
            {
                Logger.Warn(exception, "Osiris Home weekly playtime could not be read by Stats.");
                return new List<HomePlaytimeEntry>();
            }
        }

        private static int? GetFavoriteHour(IEnumerable<int> hours)
        {
            return hours
                .GroupBy(hour => hour)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .Select(group => (int?)group.Key)
                .FirstOrDefault();
        }

        private static string FormatFavoriteHour(int? hour)
        {
            if (!hour.HasValue) return "—";
            if (hour.Value == 0) return "Midnight";
            if (hour.Value == 12) return "Noon";
            return (hour.Value % 12).ToString(CultureInfo.CurrentCulture) +
                (hour.Value < 12 ? " a.m." : " p.m.");
        }

        private static string FormatWeeklyHours(ulong seconds)
        {
            if (seconds == 0UL) return "0hs";
            if (seconds < 360UL) return "<0.1hs";
            return (seconds / 3600d).ToString("0.#", CultureInfo.CurrentCulture) + "hs";
        }

        private static string FormatPlaytime(ulong seconds)
        {
            var minutes = seconds / 60UL;
            var hours = minutes / 60UL;
            minutes %= 60UL;
            if (hours == 0UL) return minutes.ToString(CultureInfo.CurrentCulture) + "m";
            return minutes == 0UL
                ? hours.ToString(CultureInfo.CurrentCulture) + "h"
                : hours.ToString(CultureInfo.CurrentCulture) + "h " +
                  minutes.ToString(CultureInfo.CurrentCulture) + "m";
        }

        private static string GetGameName(Game game)
        {
            return string.IsNullOrWhiteSpace(game?.Name) ? "Untitled game" : game.Name.Trim();
        }

        private static ulong SaturatingAdd(ulong left, ulong right)
        {
            return ulong.MaxValue - left < right ? ulong.MaxValue : left + right;
        }

        private sealed class HomePlaytimePersistence
        {
            public string GameId { get; set; }
            public string Day { get; set; }
            public string Seconds { get; set; }

            internal HomePlaytimeEntry ToEntry()
            {
                Guid gameId;
                DateTime day;
                ulong seconds;
                return Guid.TryParse(GameId, out gameId) &&
                       DateTime.TryParseExact(
                           Day,
                           "yyyy-MM-dd",
                           CultureInfo.InvariantCulture,
                           DateTimeStyles.None,
                           out day) &&
                       ulong.TryParse(
                           Seconds,
                           NumberStyles.Integer,
                           CultureInfo.InvariantCulture,
                           out seconds)
                    ? new HomePlaytimeEntry { GameId = gameId, Day = day.Date, Seconds = seconds }
                    : null;
            }
        }

        private sealed class HomePlaytimeEntry
        {
            public Guid GameId { get; set; }
            public DateTime Day { get; set; }
            public ulong Seconds { get; set; }
        }

    }

    internal sealed class StatsHomeWeeklySnapshot
    {
        public int SchemaVersion { get; set; }
        public List<StatsHomeWeeklyMetric> Metrics { get; set; }
    }

    internal sealed class StatsHomeWeeklyMetric
    {
        public string Key { get; set; }
        public string Label { get; set; }
        public string Value { get; set; }
        public string Detail { get; set; }
        public bool Available { get; set; } = true;
        public Guid GameId { get; set; }
        public List<StatsTrophyEntry> Trophies { get; set; }
        public List<StatsHomeWeeklyBar> Bars { get; set; }
    }

    internal sealed class StatsHomeWeeklyBar
    {
        public string Day { get; set; }
        public string Duration { get; set; }
        public ulong Seconds { get; set; }
        public bool IsToday { get; set; }
    }
}
