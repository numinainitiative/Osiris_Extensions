using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;

namespace Osiris.Extensions.Stats
{
    internal sealed class StatsTrophyEntry
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string GameName { get; set; }
        public string Icon { get; set; }
        public DateTime? UnlockedUtc { get; set; }
        public double? GlobalPercent { get; set; }
        public string ToolTip => GameName + " — " + Name + "\n" +
            UnlockedUtc?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
    }

    internal sealed class StatsTrophiesSnapshot
    {
        public bool Available { get; private set; }
        public int Total { get; private set; }
        public int Weekly { get; private set; }
        public List<StatsTrophyEntry> Recent { get; private set; } = new List<StatsTrophyEntry>();

        internal static StatsTrophiesSnapshot Create(bool available, IEnumerable<StatsTrophyEntry> entries, DateTime now)
        {
            var result = new StatsTrophiesSnapshot { Available = available };
            if (!available) return result;
            var earned = entries.Where(t => t != null && t.UnlockedUtc.HasValue &&
                t.UnlockedUtc.Value.ToLocalTime() <= now).ToList();
            var monday = now.Date.AddDays(-((int)now.DayOfWeek + 6) % 7);
            result.Total = earned.Count;
            result.Weekly = earned.Count(t => t.UnlockedUtc.Value.ToLocalTime() >= monday);
            result.Recent = earned.OrderByDescending(t => t.UnlockedUtc)
                .ThenBy(t => t.GlobalPercent.HasValue && t.GlobalPercent >= 0 && t.GlobalPercent <= 100
                    ? t.GlobalPercent.Value : double.MaxValue)
                .ThenBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(t => t.Id, StringComparer.Ordinal).Take(3).ToList();
            return result;
        }

        internal static StatsTrophiesSnapshot Read(IPlayniteAPI api, IEnumerable<Game> games)
        {
            var plugin = api?.Addons?.Plugins?.FirstOrDefault(p =>
                p.Id == Guid.Parse("72ba1fc9-490b-4fa4-96f0-0a154b56c1db"));
            var method = plugin?.GetType().GetMethod("GetEarnedTrophiesForStatsForOsiris", new[] { typeof(Guid) });
            var entries = new List<StatsTrophyEntry>();
            if (method != null)
            {
                foreach (var game in games)
                {
                    try
                    {
                        var json = method.Invoke(plugin, new object[] { game.Id }) as string;
                        if (!string.IsNullOrWhiteSpace(json))
                            entries.AddRange(Serialization.FromJson<List<StatsTrophyEntry>>(json) ?? new List<StatsTrophyEntry>());
                    }
                    catch (Exception error) { LogManager.GetLogger().Warn(error, "Stats could not read a game's Trophies snapshot."); }
                }
            }
            return Create(plugin != null, entries, DateTime.Now);
        }
    }
}
