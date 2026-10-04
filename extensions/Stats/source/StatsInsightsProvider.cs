using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace Osiris.Extensions.Stats
{
    public sealed class InsightRow
    {
        public string Prefix { get; private set; }
        public string Value { get; private set; }
        public string Suffix { get; private set; }
        public string Text { get; private set; }
        internal static InsightRow FromText(string text)
        {
            var prefix = text.StartsWith("Longest session was ", StringComparison.Ordinal) ? "Longest session was " :
                text.StartsWith("This game is ", StringComparison.Ordinal) ? "This game is " : text;
            var value = prefix == text ? string.Empty : text.Substring(prefix.Length).TrimEnd('.');
            return new InsightRow { Text = text, Prefix = prefix, Value = value, Suffix = value.Length == 0 ? string.Empty : "." };
        }
        public override string ToString() => Text;
    }

    internal sealed class StatsInsightsProvider
    {
        private readonly IPlayniteAPI api;
        private readonly StatsSessionLedger ledger;
        private HomeInsight homeInsight;
        private DateTime homeDate;
        private readonly Random random = new Random();
        private bool homeSelected;
        private readonly Func<StatsSettings> readSettings;
        internal StatsInsightsProvider(IPlayniteAPI api, StatsSessionLedger ledger, Func<StatsSettings> readSettings = null)
        { this.api = api; this.ledger = ledger; this.readSettings = readSettings ?? (() => new StatsSettings()); }
        internal void ResetHome() { homeSelected = false; homeInsight = null; }

        internal bool Enabled(Game game)
        {
            if (game == null || game.Hidden) return false;
            try
            {
                var path = Path.Combine(api.Database.GetFileStoragePath(game.Id), "OsirisStatsInsights.ini");
                return !File.Exists(path) || !File.ReadAllLines(path).Any(l => l.Trim().Equals("Enabled=False", StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception exception)
            {
                LogManager.GetLogger().Warn(exception, "Stats Insights settings could not be read.");
                return false;
            }
        }

        internal static string Age(DateTime? release, DateTime today)
        {
            if (!release.HasValue || release.Value.Year <= 1 || release.Value.Date > today.Date) return null;
            var years = today.Year - release.Value.Year;
            if (release.Value.Date.AddYears(years) > today.Date) years--;
            return "This game is " + years + (years == 1 ? " year old." : " years old.");
        }

        internal static string SessionText(StatsSessionRecord record, bool forGame)
        {
            if (record == null || record.Seconds == 0) return null;
            var duration = record.Seconds >= 3600 ? (record.Seconds / 3600) + "h " + (record.Seconds % 3600 / 60) + "m" :
                record.Seconds >= 60 ? (record.Seconds / 60) + "m" : record.Seconds + "s";
            return (forGame ? "Longest session was " : "Your longest recorded gaming session was ") +
                duration + (forGame ? ", on " : " long, on ") + record.StartedUtc.ToLocalTime().ToString("MMMM d, yyyy", CultureInfo.CurrentCulture) + ".";
        }

        internal List<string> ForGame(Game game)
        {
            var result = new List<string>();
            var settings = readSettings();
            if (!settings.PerGameEnabled || !Enabled(game)) return result;
            var session = ledger.Snapshot(new HashSet<Guid> { game.Id }).OrderByDescending(r => r.Seconds).FirstOrDefault();
            var text = SessionText(session, true);
            if (settings.PerGameLongestSession && text != null) result.Add(text);
            var age = Age(game.ReleaseDate?.Date, DateTime.Today);
            if (settings.PerGameAge && age != null) result.Add(age);
            return result;
        }

        internal string Home()
        {
            return SelectHome()?.Text;
        }
        internal string HomeJson()
        {
            var selected = SelectHome();
            return selected == null ? null : Playnite.SDK.Data.Serialization.ToJson(selected);
        }

        private HomeInsight SelectHome()
        {
            var settings = readSettings();
            if (!settings.GlobalEnabled || !settings.AnyGlobalEnabled) return null;
            if (homeDate != DateTime.Today) { ResetHome(); homeDate = DateTime.Today; }
            if (homeSelected) return homeInsight;
            var games = api.Database.Games.Where(g => !g.Hidden).ToList();
            var playtime = StatsGlobalPageControl.CreateStatsPlaytimeResolver();
            var candidates = GlobalInsightBuilder.Build(games, ledger.Snapshot(new HashSet<Guid>(games.Select(g => g.Id))),
                settings, DateTime.Today, playtime);
            var kinds = candidates.GroupBy(i => i.Kind).ToList();
            if (kinds.Count == 0) return null;
            var pool = kinds[random.Next(kinds.Count)].ToList();
            homeInsight = pool[random.Next(pool.Count)];
            homeSelected = homeInsight != null;
            return homeInsight;
        }
    }
}
