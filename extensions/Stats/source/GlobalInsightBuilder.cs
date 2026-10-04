using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Playnite.SDK.Models;

namespace Osiris.Extensions.Stats
{
    public sealed class HomeInsightSegment
    {
        public string Text { get; set; }
        public bool Highlight { get; set; }
    }
    public sealed class HomeInsight
    {
        public string Kind { get; set; }
        public List<HomeInsightSegment> Segments { get; set; }
        public string Text { get; set; }
    }
    internal static class GlobalInsightBuilder
    {
        private static HomeInsight Make(string kind, params string[] parts) => new HomeInsight
        {
            Kind = kind,
            Text = string.Concat(parts),
            Segments = parts.Select((text, i) => new HomeInsightSegment { Text = text, Highlight = i % 2 == 1 }).ToList()
        };
        internal static int Years(DateTime date, DateTime today)
        {
            var years = today.Year - date.Year;
            return date.Date.AddYears(years) > today.Date ? years - 1 : years;
        }
        internal static string Duration(ulong seconds) => (seconds / 3600).ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".") + "h " + (seconds % 3600 / 60) + "m";
        private static string Elapsed(DateTime date, DateTime today)
        {
            var years = Years(date, today);
            if (years > 0) return years + (years == 1 ? " year" : " years");
            var months = (today.Year - date.Year) * 12 + today.Month - date.Month;
            if (date.Date.AddMonths(months) > today.Date) months--;
            if (months > 0) return months + (months == 1 ? " month" : " months");
            var days = (today.Date - date.Date).Days;
            return days + (days == 1 ? " day" : " days");
        }
        internal static List<HomeInsight> Build(IEnumerable<Game> source, IEnumerable<StatsSessionRecord> history,
            StatsSettings settings, DateTime today, Func<Game, ulong> playtime = null)
        {
            var result = new List<HomeInsight>();
            if (!settings.GlobalEnabled) return result;
            var games = source.Where(g => g != null && !g.Hidden && !string.IsNullOrWhiteSpace(g.Name)).ToList();
            var ids = new HashSet<Guid>(games.Select(g => g.Id));
            var sessions = history.Where(s => ids.Contains(s.GameId) && s.Seconds > 0 && s.StartedUtc.ToLocalTime().Date <= today.Date).ToList();
            var times = games.ToDictionary(g => g.Id, g => playtime == null ? g.Playtime : playtime(g));
            if (settings.GlobalLongestSession)
            {
                var longest = sessions.OrderByDescending(s => s.Seconds).FirstOrDefault();
                if (longest != null)
                {
                    var value = longest.Seconds >= 3600 ? Duration(longest.Seconds) : longest.Seconds >= 60 ? longest.Seconds / 60 + "m" : longest.Seconds + "s";
                    result.Add(Make("LongestSession", "Your longest recorded gaming session was ", value,
                        " long, on ", longest.StartedUtc.ToLocalTime().ToString("MMMM d, yyyy", CultureInfo.CurrentCulture), "."));
                }
            }
            if (settings.GlobalMostPlayed)
            {
                var top = games.Where(g => times[g.Id] > 0).OrderByDescending(g => times[g.Id]).ThenBy(g => g.Name).FirstOrDefault();
                if (top != null) result.Add(Make("MostPlayed", "Your most played game is ", top.Name, ", with a total of ", Duration(times[top.Id]), " of playtime."));
            }
            var released = games.Where(g => g.ReleaseDate.HasValue && g.ReleaseDate.Value.Date.Year > 1 && g.ReleaseDate.Value.Date.Date <= today.Date).ToList();
            if (settings.GlobalAnniversaries)
                foreach (var game in released)
                {
                    if (!game.ReleaseDate.Value.Month.HasValue || !game.ReleaseDate.Value.Day.HasValue) continue;
                    var date = game.ReleaseDate.Value.Date;
                    var years = Years(date, today);
                    if (years > 0 && date.Month == today.Month && date.Day == today.Day)
                        result.Add(Make("Anniversaries", "Today is ", game.Name + (game.Name.EndsWith("s", StringComparison.OrdinalIgnoreCase) ? "' " : "'s ") + years + " year anniversary", "."));
                }
            if (settings.GlobalOldestGame)
            {
                var oldest = released.OrderBy(g => g.ReleaseDate.Value.Date).ThenBy(g => g.Name).FirstOrDefault();
                if (oldest != null) result.Add(Make("OldestGame", "The oldest game in your library is ", oldest.Name, ", released ", Elapsed(oldest.ReleaseDate.Value.Date, today) + " ago", "."));
            }
            if (settings.GlobalTotalPlaytime)
            {
                ulong total = 0;
                foreach (var seconds in times.Values) total = ulong.MaxValue - total < seconds ? ulong.MaxValue : total + seconds;
                if (total > 0) result.Add(Make("TotalPlaytime", "You've accumulated a total of ", Duration(total), " of game time."));
            }
            if (settings.GlobalUnplayedReminder)
                foreach (var game in games.Where(g => g.PlayCount == 0 && g.Playtime == 0 && times[g.Id] == 0 && !g.LastActivity.HasValue &&
                    !sessions.Any(s => s.GameId == g.Id) && g.Added.HasValue && g.Added.Value.ToLocalTime().Date < today.Date))
                    result.Add(Make("UnplayedReminder", "", game.Name, " has been in your library for ", Elapsed(game.Added.Value.ToLocalTime(), today), " now. When do you plan to play it?"));
            if (settings.GlobalFirstPlayedBirthday)
                foreach (var first in sessions.GroupBy(s => s.GameId).Select(group => group.OrderBy(s => s.StartedUtc).First()))
                {
                    var date = first.StartedUtc.ToLocalTime();
                    var years = Years(date, today);
                    if (years > 0 && date.Month == today.Month && date.Day == today.Day)
                        result.Add(Make("FirstPlayedBirthday", "Today marks ", years + (years == 1 ? " year since" : " years since"),
                            " your first recorded play of ", games.First(g => g.Id == first.GameId).Name, " on Osiris."));
                }
            return result;
        }
    }
}
