using System;
using System.Linq;
using System.Collections.Generic;
using Playnite.SDK.Models;
using Osiris.Extensions.Stats;

internal static class InsightsValidation
{
    static int checks;
    static void Check(bool value) { checks++; if (!value) throw new Exception("Insight check " + checks + " failed"); }
    static void ValidateGlobalInsights()
    {
        var today = new DateTime(2026, 10, 3);
        var played = new Game { Name = "The Last Of Us", Playtime = 722UL * 3600 + 45 * 60, PlayCount = 4,
            ReleaseDate = new ReleaseDate(2014, 10, 3) };
        var oldest = new Game { Name = "Need for Speed Most Wanted", ReleaseDate = new ReleaseDate(2005, 3, 20), PlayCount = 1 };
        var unplayed = new Game { Name = "Hello Neighbour", Added = new DateTime(2023, 9, 3) };
        var hidden = new Game { Name = "Hidden", Hidden = true, Playtime = ulong.MaxValue, ReleaseDate = new ReleaseDate(1990, 10, 3) };
        var future = new Game { Name = "Future", Added = new DateTime(2030, 1, 1), ReleaseDate = new ReleaseDate(2030, 10, 3) };
        var games = new[] { played, oldest, unplayed, hidden, future };
        var sessions = new[] { new StatsSessionRecord { GameId = played.Id, Seconds = 13500, StartedUtc = new DateTime(2023,10,3,12,0,0,DateTimeKind.Utc) } };
        var settings = new StatsSettings();
        var all = GlobalInsightBuilder.Build(games, sessions, settings, today);
        Check(all.Select(i => i.Kind).Distinct().Count() == 7);
        Check(all.Single(i => i.Kind == "MostPlayed").Text == "Your most played game is The Last Of Us, with a total of 722h 45m of playtime.");
        Check(all.Single(i => i.Kind == "Anniversaries").Text.Contains("12 year anniversary"));
        Check(all.Single(i => i.Kind == "OldestGame").Text.Contains("21 years ago"));
        Check(all.Single(i => i.Kind == "UnplayedReminder").Text.Contains("Hello Neighbour has been in your library for 3 years"));
        Check(all.Single(i => i.Kind == "FirstPlayedBirthday").Text.Contains("3 years since your first recorded play of The Last Of Us"));
        Check(all.Single(i => i.Kind == "TotalPlaytime").Text.Contains("722h 45m"));
        Check(all.All(i => !i.Text.Contains("Hidden") && !i.Text.Contains("Future")));
        Check(all.All(i => i.Segments.Any(s => s.Highlight) && i.Text == string.Concat(i.Segments.Select(s => s.Text))));
        var topHighlights = all.Single(i => i.Kind == "MostPlayed").Segments.Where(s => s.Highlight).Select(s => s.Text).ToArray();
        Check(topHighlights.SequenceEqual(new[] { "The Last Of Us", "722h 45m" }));
        var payload = Newtonsoft.Json.JsonConvert.SerializeObject(all.Single(i => i.Kind == "MostPlayed"));
        Check(Newtonsoft.Json.Linq.JObject.Parse(payload)["Text"].ToString() == all.Single(i => i.Kind == "MostPlayed").Text);
        Check(Newtonsoft.Json.Linq.JObject.Parse(payload)["Segments"].Count() == 5);
        Check(GlobalInsightBuilder.Duration(3280UL * 3600 + 34 * 60) == "3.280h 34m");
        foreach (var mapping in new[] { "GlobalLongestSession:LongestSession", "GlobalMostPlayed:MostPlayed", "GlobalAnniversaries:Anniversaries",
            "GlobalOldestGame:OldestGame", "GlobalTotalPlaytime:TotalPlaytime", "GlobalUnplayedReminder:UnplayedReminder", "GlobalFirstPlayedBirthday:FirstPlayedBirthday" })
        {
            var parts = mapping.Split(':');
            var preference = typeof(StatsSettings).GetProperty(parts[0]);
            preference.SetValue(settings, false, null);
            Check(!GlobalInsightBuilder.Build(games, sessions, settings, today).Any(i => i.Kind == parts[1]));
            Check(!(bool)preference.GetValue(settings.Clone(), null));
            preference.SetValue(settings, true, null);
        }
        settings.GlobalEnabled = false;
        Check(GlobalInsightBuilder.Build(null, null, settings, today).Count == 0);
        Check(settings.GlobalMostPlayed && settings.GlobalLongestSession);
        settings.GlobalEnabled = true;
        var tomorrow = GlobalInsightBuilder.Build(games, sessions, settings, today.AddDays(1));
        Check(!tomorrow.Any(i => i.Kind == "Anniversaries" || i.Kind == "FirstPlayedBirthday"));
        unplayed.PlayCount = 1;
        Check(!GlobalInsightBuilder.Build(games, sessions, settings, today).Any(i => i.Kind == "UnplayedReminder"));
        var unified = GlobalInsightBuilder.Build(games, sessions, settings, today, g => g.Id == oldest.Id ? 1000UL * 3600 : g.Playtime);
        Check(unified.Single(i => i.Kind == "MostPlayed").Text.Contains(oldest.Name));
        Check(unified.Single(i => i.Kind == "TotalPlaytime").Text.Contains("1.722h 45m"));
        Check(GlobalInsightBuilder.Build(new Game[0], new StatsSessionRecord[0], settings, today).Count == 0);
        var leap = new Game { Name = "Leap", ReleaseDate = new ReleaseDate(2020,2,29) };
        Check(!GlobalInsightBuilder.Build(new[] { leap }, new StatsSessionRecord[0], settings, new DateTime(2026,2,28)).Any(i => i.Kind == "Anniversaries"));
        Check(GlobalInsightBuilder.Build(new[] { leap }, new StatsSessionRecord[0], settings, new DateTime(2028,2,29)).Any(i => i.Kind == "Anniversaries"));
        var partial = new Game { Name = "Year only", ReleaseDate = new ReleaseDate(2020) };
        Check(!GlobalInsightBuilder.Build(new[] { partial }, new StatsSessionRecord[0], settings, new DateTime(2026,1,1)).Any(i => i.Kind == "Anniversaries"));
    }
    [STAThread]
    static void Main()
    {
        var today = new DateTime(2026, 10, 3);
        Check(StatsInsightsProvider.Age(null, today) == null);
        Check(StatsInsightsProvider.Age(new DateTime(2027,10,3), today) == null);
        Check(StatsInsightsProvider.Age(new DateTime(1,10,3), today) == null);
        Check(StatsInsightsProvider.Age(new DateTime(2026,10,3), today) == "This game is 0 years old.");
        Check(StatsInsightsProvider.Age(new DateTime(2025,10,3), today) == "This game is 1 year old.");
        Check(StatsInsightsProvider.Age(new DateTime(2014,10,3), today) == "This game is 12 years old.");
        Check(StatsInsightsProvider.Age(new DateTime(2014,10,4), today) == "This game is 11 years old.");
        Check(StatsInsightsProvider.Age(new DateTime(2014,10,2), today) == "This game is 12 years old.");
        Check(StatsInsightsProvider.Age(new DateTime(2020,2,29), new DateTime(2026,2,28)) == "This game is 6 years old.");
        Check(StatsInsightsProvider.SessionText(null, false) == null);
        Check(StatsInsightsProvider.SessionText(new StatsSessionRecord(), true) == null);
        var record = new StatsSessionRecord { Seconds = 13500, StartedUtc = new DateTime(2026,3,23,12,0,0,DateTimeKind.Utc) };
        Check(StatsInsightsProvider.SessionText(record, false).Contains("3h 45m"));
        Check(StatsInsightsProvider.SessionText(record, true).StartsWith("Longest session was 3h 45m, on "));
        record.Seconds = 45;
        Check(StatsInsightsProvider.SessionText(record, false).Contains("45s"));
        record.Seconds = 120;
        Check(StatsInsightsProvider.SessionText(record, false).Contains("2m"));
        Check(new StatsInsightsProvider(null, null).ForGame(null).Count == 0);
        var row = InsightRow.FromText("Longest session was 22s, on October 2, 2026.");
        Check(row.Prefix == "Longest session was " && row.Value == "22s, on October 2, 2026" && row.Suffix == ".");
        Check(row.ToString() == "Longest session was 22s, on October 2, 2026.");
        row = InsightRow.FromText("This game is 9 years old.");
        Check(row.Prefix == "This game is " && row.Value == "9 years old" && row.Suffix == ".");
        row = InsightRow.FromText("An unknown insight.");
        Check(row.Prefix == row.Text && row.Value == string.Empty);
        var settings = new StatsSettings();
        Check(settings.GlobalEnabled && settings.GlobalLongestSession && settings.PerGameEnabled && settings.PerGameLongestSession && settings.PerGameAge);
        var editing = settings.Clone();
        editing.PerGameAge = false;
        editing.GlobalEnabled = false;
        Check(settings.PerGameAge && settings.GlobalEnabled);
        Check(!editing.Clone().PerGameAge && !editing.Clone().GlobalEnabled);
        var disabled = new StatsInsightsProvider(null, null, () => editing);
        Check(disabled.Home() == null);
        editing.GlobalEnabled = true;
        editing.GlobalLongestSession = false;
        editing.GlobalMostPlayed = editing.GlobalAnniversaries = editing.GlobalOldestGame = editing.GlobalTotalPlaytime =
            editing.GlobalUnplayedReminder = editing.GlobalFirstPlayedBirthday = false;
        Check(disabled.Home() == null);
        editing.PerGameEnabled = false;
        Check(disabled.ForGame(new Playnite.SDK.Models.Game()) .Count == 0);
        var view = new StatsSettingsView();
        var tabs = (System.Windows.Controls.TabControl)view.Content;
        Check(tabs.Items.Count == 3);
        Check(((System.Windows.Controls.TabItem)tabs.Items[1]).Header.ToString() == "Global");
        Check(((System.Windows.Controls.TabItem)tabs.Items[2]).Header.ToString() == "Per Game");
        var switches = 0;
        foreach (System.Windows.Controls.TabItem tab in tabs.Items)
        {
            var page = (System.Windows.Controls.StackPanel)tab.Content;
            Check(page.Width == 760 && page.HorizontalAlignment == System.Windows.HorizontalAlignment.Left);
            foreach (var child in page.Children)
            {
                var grid = child as System.Windows.Controls.Grid;
                if (grid == null) continue;
                var toggle = (System.Windows.Controls.CheckBox)grid.Children[0];
                toggle.ApplyTemplate();
                Check(toggle.Width == 44 && toggle.Height == 24 && grid.Height == 54 && grid.ColumnDefinitions[0].Width.Value == 56);
                Check(System.Windows.Data.BindingOperations.GetBinding(toggle, System.Windows.UIElement.IsEnabledProperty) == null);
                switches++;
            }
        }
        Check(switches == 11);
        ValidateGlobalInsights();
        Console.WriteLine(checks + " insight checks passed.");
    }
}
