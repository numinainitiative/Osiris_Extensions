using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Osiris.Extensions.Exophase;
using Playnite.SDK.Models;

internal static class Program
{
    private static int checks;

    [STAThread]
    private static int Main()
    {
        try
        {
            RunChecks();
            Console.WriteLine($"PASS checks={checks}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL " + exception);
            return 1;
        }
    }

    private static void RunChecks()
    {
        var settings = new ExophaseSettings();
        Expect(!settings.ConnectAccount, "Account connection should require an explicit opt-in.");
        Expect(settings.ImportAchievements && settings.ImportPlayTime && settings.ImportPlatforms,
            "All supported import categories should be selected by default.");
        Expect(settings.SyncOnStartup, "Startup synchronisation should be selected by default.");
        Expect(!settings.OverrideDisplayedPlaytime,
            "Total Time Played should require an explicit display opt-in.");

        settings.BeginEdit();
        settings.ConnectAccount = true;
        settings.ImportPlayTime = false;
        settings.ProfileReference = "TestAgent";
        List<string> errors;
        Expect(settings.VerifySettings(out errors) && errors.Count == 0,
            "Foundation preferences should validate without requiring credentials.");
        settings.CancelEdit();
        Expect(!settings.ConnectAccount && settings.ImportPlayTime &&
               string.IsNullOrEmpty(settings.ProfileReference),
            "Cancelling settings should restore the original values.");

        var settingsView = new ExophaseSettingsView();
        var tabs = settingsView.Content as TabControl;
        var tabItems = tabs?.Items.Cast<TabItem>().ToList();
        Expect(tabItems != null && tabItems.Count == 3,
            "Exophase should expose exactly three settings categories.");
        Expect(string.Equals(tabItems[0].Header as string, "Account", StringComparison.Ordinal) &&
               string.Equals(tabItems[1].Header as string, "General", StringComparison.Ordinal) &&
               string.Equals(tabItems[2].Header as string, "Danger Zone", StringComparison.Ordinal),
            "Account, General, and Danger Zone should use the requested order.");
        Expect(settingsView.FindName("ConnectAccountToggle") is CheckBox &&
               settingsView.FindName("AuthenticateButton") is Button &&
               settingsView.FindName("SignOutButton") is Button &&
               settingsView.FindName("ProfileReferenceTextBox") is TextBox &&
               settingsView.FindName("UpdateDatabaseButton") is Button settingsUpdateButton &&
               string.Equals(settingsUpdateButton.Content as string, "Update Database", StringComparison.Ordinal) &&
               settingsView.FindName("OsirisExtensionDangerZone") is Grid,
            "The settings view should expose account, extractor, and danger-zone actions.");

        Expect(ExophaseAuthenticationService.IsAuthenticatedAddress("https://www.exophase.com/account/") &&
               ExophaseAuthenticationService.IsAuthenticatedAddress("https://exophase.com/account/connected-platforms/") &&
               !ExophaseAuthenticationService.IsAuthenticatedAddress("https://www.exophase.com/login/") &&
               !ExophaseAuthenticationService.IsAuthenticatedAddress("https://example.com/account/"),
            "Authentication completion should accept only Exophase account pages.");

        var extensionRoot = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", ".."));
        var viewSource = File.ReadAllText(Path.Combine(extensionRoot, "source", "ExophaseSettingsView.xaml"));
        var authSource = File.ReadAllText(Path.Combine(extensionRoot, "source", "ExophaseAuthenticationService.cs"));
        var globalPageView = File.ReadAllText(Path.Combine(extensionRoot, "source", "ExophaseGlobalPageControl.xaml"));
        var globalPageCode = File.ReadAllText(Path.Combine(extensionRoot, "source", "ExophaseGlobalPageControl.cs"));
        var platformTimelineCode = File.ReadAllText(Path.Combine(extensionRoot, "source", "ExophasePlatformTimeline.cs"));
        var platformWheelCode = File.ReadAllText(Path.Combine(extensionRoot, "source", "ExophasePlatformWheel.cs"));
        var globalPagePluginCode = File.ReadAllText(Path.Combine(extensionRoot, "source", "ExophasePlugin.cs"));
        Expect(viewSource.Contains("Width=\"760\"") &&
               viewSource.Contains("Width=\"44\" Height=\"24\"") &&
               viewSource.Contains("OverrideDisplayedPlaytime") &&
               viewSource.Contains("SyncOnStartup") &&
               viewSource.Contains("Display Total Time Played") &&
               viewSource.Contains("Synchronise the platforms and played time for Osiris games") &&
               !viewSource.Contains("ImportAchievements, Mode=TwoWay") &&
               !viewSource.Contains("ImportPlayTime, Mode=TwoWay") &&
               !viewSource.Contains("ImportPlatforms, Mode=TwoWay") &&
               !viewSource.Contains("Text=\"{Binding SynchronizationStatus}\"") &&
               !viewSource.Contains("Osiris keeps an offline Exophase snapshot") &&
               viewSource.Contains("x:Name=\"OsirisExtensionDangerZone\"") &&
               !viewSource.Contains("PasswordBox"),
            "Settings should expose only the two final preferences, the concise synchronisation action, canonical dimensions, and no password field.");
        Expect(authSource.Contains("CreateView(1225, 700)") &&
               authSource.Contains("https://www.exophase.com/account/") &&
               authSource.Contains("DeleteDomainCookies") &&
               !authSource.Contains("Password"),
            "Authentication should use the Middle Window webview and cookie-only session handling.");

        var globalPage = new ExophaseGlobalPageSidebarItem();
        var globalView = globalPage.Opened?.Invoke() as ExophaseGlobalPageControl;
        var globalViewLayout = globalView?.Content as Grid;
        var globalViewHeader = globalViewLayout?.Children
            .OfType<FrameworkElement>()
            .FirstOrDefault(element => Grid.GetRow(element) == 0);
        var globalGamesGrid = globalView?.FindName("GamesGrid") as DataGrid;
        var globalSortByCombo = globalView?.FindName("SortByCombo") as ComboBox;
        var globalUpdateDatabaseButton = globalView?.FindName("UpdateDatabaseButton") as Button;
        var globalLastDatabaseUpdateText = globalView?.FindName("LastDatabaseUpdateText") as TextBlock;
        var globalSortToolbarGroup = globalView?.FindName("SortToolbarGroup") as StackPanel;
        var globalUpdateToolbarGroup = globalView?.FindName("UpdateToolbarGroup") as StackPanel;
        var globalPageScrollViewer = globalView?.FindName("PageScrollViewer") as ScrollViewer;
        var globalScrollToolbar = globalView?.FindName("ScrollToolbar") as Grid;
        var globalGamesTableContainer = globalView?.FindName("GamesTableContainer") as Border;
        var tableLegendBar = globalView?.FindName("TableLegendBar") as Grid;
        var fetchedGamesCountText = globalView?.FindName("FetchedGamesCountText") as TextBlock;
        var noExophaseDataCountText = globalView?.FindName("NoExophaseDataCountText") as TextBlock;
        var disabledGamesCountText = globalView?.FindName("DisabledGamesCountText") as TextBlock;
        var fetchedGamesCountBrush = fetchedGamesCountText?.Foreground as System.Windows.Media.SolidColorBrush;
        var noExophaseDataCountBrush = noExophaseDataCountText?.Foreground as System.Windows.Media.SolidColorBrush;
        var disabledGamesCountBrush = disabledGamesCountText?.Foreground as System.Windows.Media.SolidColorBrush;
        var globalStatisticsCard = globalView?.FindName("GlobalStatisticsCard") as Border;
        var globalPlatformWheel = globalView?.FindName("GlobalPlatformWheel") as ExophasePlatformWheel;
        var platformStatisticsItems = globalView?.FindName("PlatformStatisticsItems") as ItemsControl;
        var globalTotalPlaytimeValue = globalView?.FindName("GlobalTotalPlaytimeValue") as TextBlock;
        var globalTotalTrophiesValue = globalView?.FindName("GlobalTotalTrophiesValue") as TextBlock;
        var globalPlatformsUsedValue = globalView?.FindName("GlobalPlatformsUsedValue") as TextBlock;
        Expect(globalPage.OsirisGlobalPage &&
               globalPage.Type == Playnite.SDK.Plugins.SiderbarItemType.View &&
               string.Equals(globalPage.Title, "Exophase", StringComparison.Ordinal) &&
               globalView != null &&
               globalViewLayout != null &&
               globalViewLayout.RowDefinitions.Count == 2 &&
               globalViewLayout.RowDefinitions[0].Height.IsAbsolute &&
               Math.Abs(globalViewLayout.RowDefinitions[0].Height.Value - 76) < 0.01 &&
               globalViewHeader != null &&
               globalViewHeader.Visibility == Visibility.Visible,
            "Exophase should opt into the Osiris extension-page list using the fixed HLTB page shell.");
        Expect(globalGamesGrid != null &&
               !globalGamesGrid.AutoGenerateColumns &&
               globalGamesGrid.Columns.Count == 4 &&
               globalGamesGrid.EnableRowVirtualization &&
               globalGamesGrid.EnableColumnVirtualization &&
               string.Equals(globalGamesGrid.Columns[0].Header as string, "ICON", StringComparison.Ordinal) &&
               globalGamesGrid.Columns[0].Width.IsAbsolute &&
               Math.Abs(globalGamesGrid.Columns[0].Width.Value - 94) < 0.01 &&
               string.Equals(globalGamesGrid.Columns[1].Header as string, "GAME", StringComparison.Ordinal) &&
               globalGamesGrid.Columns[1].Width.IsAbsolute &&
               Math.Abs(globalGamesGrid.Columns[1].Width.Value - 200) < 0.01 &&
               string.Equals(
                   globalGamesGrid.Columns[2].Header as string,
                   "TOTAL PLAYTIME",
                   StringComparison.Ordinal) &&
               globalGamesGrid.Columns[2].Width.IsAbsolute &&
               Math.Abs(globalGamesGrid.Columns[2].Width.Value - 140) < 0.01 &&
               string.Equals(
                   globalGamesGrid.Columns[3].Header as string,
                   "PLAY TIME BY PLATFORM",
                   StringComparison.Ordinal),
            "The Exophase page should use a square icon field, game and total-playtime fields, and a flexible platform-time field.");
        Expect(globalSortByCombo != null &&
               globalSortByCombo.Items.Count == 2 &&
               string.Equals(((ComboBoxItem)globalSortByCombo.Items[0]).Content as string, "Title", StringComparison.Ordinal) &&
               string.Equals(((ComboBoxItem)globalSortByCombo.Items[1]).Content as string, "Total Playtime", StringComparison.Ordinal) &&
               globalView.FindName("ShowOnlyPlayedToggle") == null &&
               globalUpdateDatabaseButton != null &&
               string.Equals(globalUpdateDatabaseButton.Content as string, "Update Database", StringComparison.Ordinal) &&
               globalLastDatabaseUpdateText != null &&
               string.Equals(globalLastDatabaseUpdateText.Text, "Never updated", StringComparison.Ordinal) &&
               globalSortToolbarGroup != null &&
               globalUpdateToolbarGroup != null &&
               Math.Abs(globalSortToolbarGroup.Height - 38) < 0.01 &&
               Math.Abs(globalUpdateToolbarGroup.Height - 38) < 0.01 &&
               globalSortToolbarGroup.VerticalAlignment == VerticalAlignment.Top &&
               globalUpdateToolbarGroup.VerticalAlignment == VerticalAlignment.Top &&
               globalScrollToolbar != null &&
               Math.Abs(globalScrollToolbar.Height - 66) < 0.01 &&
               !globalPageView.Contains("HOW LONG TO BEAT") &&
               !globalPageView.Contains("HowLongToBeatTimeline") &&
               !globalPageView.Contains("Show only played games") &&
               globalPageView.Contains("ExophasePlatformTimeline") &&
               globalPageView.Contains("Segments=\"{Binding PlatformSegments}\"") &&
               globalPageView.Contains("TrophyProgressRatio=\"{Binding TrophyProgressRatio}\"") &&
               globalPageView.Contains("Text=\"{Binding TrophyCountText, Mode=OneWay}\"") &&
               globalPageView.Contains("Text=\"{Binding PlayedTimeText, Mode=OneWay}\"") &&
               globalPageView.Contains("Text=\"{Binding PlatformStatusText}\"") &&
               globalPageCode.Contains("OnGameTitleClick") &&
               globalPageCode.Contains("AttachSearchBox") &&
               globalPageCode.Contains("activityResolver.Resolve(game, true)") &&
               globalPageCode.Contains("ExophasePlatformVisualCatalog.Resolve(platform.Platform)") &&
               globalPageCode.Contains("await settings.SynchronizeAsync()") &&
               globalPageCode.Contains("activityStore?.GetSnapshotInfo()") &&
               globalPagePluginCode.Contains("yield return globalPage"),
            "The Exophase toolbar should retain Title and Total Playtime sorting, add the shared database update action and timestamp, and keep search and game navigation integrated.");
        var formatReferenceNow = new DateTime(2026, 9, 14, 18, 0, 0);
        Expect(ExophaseGlobalPageControl.FormatLastUpdated(DateTime.MinValue, formatReferenceNow) ==
                   "Never updated" &&
               ExophaseGlobalPageControl.FormatLastUpdated(
                   new DateTime(2026, 9, 14, 15, 43, 0),
                   formatReferenceNow) == "Last updated today at 15:43" &&
               ExophaseGlobalPageControl.FormatLastUpdated(
                   new DateTime(2026, 9, 13, 15, 43, 0),
                   formatReferenceNow) == "Last updated yesterday at 15:43",
            "The Exophase page should show a friendly persisted last-database-update timestamp beside its action.");
        var platformStatistics = ExophaseGlobalPageControl.AggregatePlatformStatistics(
            new[]
            {
                new ExophaseTimelineSegment { Platform = "Steam", PlaytimeSeconds = 3600 },
                new ExophaseTimelineSegment { Platform = "PlayStation 4", PlaytimeSeconds = 3600 },
                new ExophaseTimelineSegment { Platform = "Steam", PlaytimeSeconds = 7200 }
             });
        var osirisStatistics = ExophaseGlobalPageControl.AggregatePlatformStatistics(
            new[]
            {
                new ExophaseTimelineSegment { Platform = "Osiris", PlaytimeSeconds = 3600 }
            });
        var osirisTagBrush = osirisStatistics.Single().TagBrush as System.Windows.Media.SolidColorBrush;
        var osirisForegroundBrush = osirisStatistics.Single().LegendForegroundBrush as System.Windows.Media.SolidColorBrush;
        var trophyRows = new[]
        {
            new ExophaseGameRow(
                new Game { Name = "Battlefield 2042" },
                null,
                null,
                0,
                "19/34",
                19,
                34,
                19d / 34d,
                new List<ExophaseTimelineSegment>(),
                string.Empty,
                ExophaseGameDataState.Fetched),
            new ExophaseGameRow(
                new Game { Name = "God of War" },
                null,
                null,
                0,
                "37/37",
                37,
                37,
                1d,
                new List<ExophaseTimelineSegment>(),
                string.Empty,
                ExophaseGameDataState.Disabled),
            new ExophaseGameRow(
                new Game { Name = "No Exophase Data" },
                null,
                null,
                0,
                "—",
                0,
                0,
                0d,
                new List<ExophaseTimelineSegment>(),
                "No Exophase data")
        };
        var gameDataSummary = ExophaseGlobalPageControl.CalculateGameDataSummary(trophyRows);
        var hoverSegments = new[]
        {
            new ExophasePlatformSummaryItem { Percentage = 0.25d },
            new ExophasePlatformSummaryItem { Percentage = 0.50d },
            new ExophasePlatformSummaryItem { Percentage = 0.25d }
        };
        var legendHighlight = new ExophasePlatformSummaryItem
        {
            Platform = "Steam",
            TagBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(52, 119, 154))
        };
        var originalLegendBrush = legendHighlight.LegendTagBrush;
        legendHighlight.SetMuted(true);
        var mutedLegendBrush = legendHighlight.LegendTagBrush;
        var mutedLegendOpacity = legendHighlight.LegendOpacity;
        legendHighlight.SetMuted(false);
        Expect(platformStatistics.Count == 2 &&
               platformStatistics[0].Platform == "Steam" &&
               platformStatistics[0].PlaytimeSeconds == 10800UL &&
               platformStatistics[0].PercentageText == "(75%)" &&
               platformStatistics[1].Platform == "PlayStation 4" &&
               platformStatistics[1].PlaytimeSeconds == 3600UL &&
               platformStatistics[1].PercentageText == "(25%)" &&
               globalStatisticsCard != null &&
               Grid.GetColumn(globalStatisticsCard) == 0 &&
               globalStatisticsCard.Parent is Grid statisticsColumns &&
               statisticsColumns.ColumnDefinitions.Count == 2 &&
               statisticsColumns.ColumnDefinitions[0].Width.IsAbsolute &&
               Math.Abs(statisticsColumns.ColumnDefinitions[0].Width.Value - 660) < 0.01 &&
               Math.Abs(statisticsColumns.MinHeight) < 0.01 &&
               globalPlatformWheel != null &&
               Math.Abs(globalPlatformWheel.Width - 400) < 0.01 &&
               Math.Abs(globalPlatformWheel.Height - 400) < 0.01 &&
               tableLegendBar != null &&
               Math.Abs(tableLegendBar.Height - 62) < 0.01 &&
               Math.Abs(tableLegendBar.Margin.Left - 24) < 0.01 &&
               Math.Abs(tableLegendBar.Margin.Right - 24) < 0.01 &&
               Math.Abs(tableLegendBar.Margin.Bottom - 18) < 0.01 &&
               string.Equals(
                   TextElement.GetFontFamily(tableLegendBar).Source,
                   globalLastDatabaseUpdateText.FontFamily.Source,
                   StringComparison.OrdinalIgnoreCase) &&
               Math.Abs(TextElement.GetFontSize(tableLegendBar) - 17) < 0.01 &&
               Math.Abs(TextElement.GetFontSize(tableLegendBar) - globalLastDatabaseUpdateText.FontSize) < 0.01 &&
               TextElement.GetFontWeight(tableLegendBar) == globalLastDatabaseUpdateText.FontWeight &&
               TextElement.GetFontWeight(tableLegendBar) == FontWeights.SemiBold &&
               fetchedGamesCountText != null &&
               noExophaseDataCountText != null &&
               disabledGamesCountText != null &&
               string.Equals(
                   fetchedGamesCountText.Text,
                   "0 Games with Exophase data",
                   StringComparison.Ordinal) &&
               string.Equals(
                   noExophaseDataCountText.Text,
                   "0 With no Exophase data",
                   StringComparison.Ordinal) &&
               string.Equals(
                   disabledGamesCountText.Text,
                   "0 Games with Exophase manually disabled",
                   StringComparison.Ordinal) &&
               fetchedGamesCountBrush != null &&
               fetchedGamesCountBrush.Color == System.Windows.Media.Color.FromRgb(119, 122, 130) &&
               noExophaseDataCountBrush != null &&
               noExophaseDataCountBrush.Color == System.Windows.Media.Color.FromRgb(119, 122, 130) &&
               disabledGamesCountBrush != null &&
               disabledGamesCountBrush.Color == System.Windows.Media.Color.FromRgb(119, 122, 130) &&
               gameDataSummary.Fetched == 1 &&
               gameDataSummary.NoData == 1 &&
               gameDataSummary.Disabled == 1 &&
               globalTotalPlaytimeValue != null &&
               globalTotalTrophiesValue != null &&
               globalPlatformsUsedValue != null &&
               platformStatisticsItems != null &&
               Math.Abs(platformStatisticsItems.Width - 620) < 0.01 &&
               platformStatisticsItems.Parent is Grid &&
               globalScrollToolbar != null &&
               globalScrollToolbar.Parent is StackPanel scrollContent &&
               ReferenceEquals(globalPageScrollViewer.Content, scrollContent) &&
               !ReferenceEquals(originalLegendBrush, mutedLegendBrush) &&
               Math.Abs(mutedLegendOpacity - 0.58d) < 0.001 &&
               ReferenceEquals(legendHighlight.LegendTagBrush, originalLegendBrush) &&
               Math.Abs(legendHighlight.LegendOpacity - 1d) < 0.001 &&
               osirisStatistics.Single().UseOsirisLogo &&
               osirisTagBrush != null &&
               osirisTagBrush.Color == System.Windows.Media.Color.FromRgb(240, 241, 247) &&
               osirisForegroundBrush != null &&
               osirisForegroundBrush.Color == System.Windows.Media.Color.FromRgb(9, 9, 9) &&
               ExophaseGlobalPageControl.FormatTotalTrophies(trophyRows) == "56/71" &&
               globalPageScrollViewer != null &&
               globalPageScrollViewer.VerticalScrollBarVisibility == ScrollBarVisibility.Auto &&
               globalGamesTableContainer != null &&
               Math.Abs(ExophaseGlobalPageControl.CalculateGamesTableHeight(900) - 720) < 0.01 &&
               Math.Abs(ExophaseGlobalPageControl.CalculateGamesTableHeight(500) - 520) < 0.01 &&
               globalPageView.Contains("x:Name=\"GlobalPlatformWheel\"") &&
               globalPageView.Contains("Text=\"PLAYTIME STATISTICS\"") &&
               globalPageView.Contains("Text=\"Trophy progress\"") &&
               globalPageView.Contains("Text=\"All trophies achieved\"") &&
               globalPageView.Contains("Text=\"0 Games with Exophase data\"") &&
               globalPageView.Contains("Text=\"0 With no Exophase data\"") &&
               globalPageView.Contains("Text=\"0 Games with Exophase manually disabled\"") &&
               !globalPageView.Contains("Fill=\"#318DB4\"") &&
               globalPageView.Contains("Fill=\"#F0F1F7\"") &&
               globalPageCode.Contains("\" Games with Exophase data\"") &&
               globalPageCode.Contains("\" With no Exophase data\"") &&
               globalPageCode.Contains("\" Games with Exophase manually disabled\"") &&
               globalPageView.Contains("ItemsSource=\"{Binding PlatformStatistics}\"") &&
               !globalPageView.Contains("<Grid MinHeight=\"780\">") &&
               globalPageView.Contains("<ColumnDefinition Width=\"660\" />") &&
               globalPageView.Contains("<ColumnDefinition Width=\"420\" />") &&
               globalPageView.Contains("<ColumnDefinition Width=\"200\" />") &&
               globalPageView.Contains("<RowDefinition Height=\"460\" />") &&
               globalPageView.Contains("<UniformGrid Columns=\"2\" />") &&
               !globalPageView.Contains("Path=(ItemsControl.AlternationIndex)") &&
               globalPageView.Contains("x:Name=\"PlatformStatisticsItems\"") &&
               globalPageView.Contains("x:Name=\"GlobalTotalPlaytimeValue\"") &&
               globalPageView.Contains("x:Name=\"GlobalTotalTrophiesValue\"") &&
               globalPageView.Contains("x:Name=\"GlobalPlatformsUsedValue\"") &&
               globalPageView.Contains("MouseEnter=\"OnPlatformLegendMouseEnter\"") &&
               globalPageView.Contains("Background=\"{Binding LegendTagBrush}\"") &&
               globalPageView.Contains("Fill=\"{Binding LegendForegroundBrush}\"") &&
               globalPageView.Contains("Foreground=\"{Binding LegendForegroundBrush}\"") &&
               globalPageCode.Contains("OnWheelHoveredPlatformChanged") &&
               globalPageCode.Contains("SetHighlightedPlatform") &&
               globalPageCode.Contains("PageScrollViewer.ActualHeight - toolbarHeight") &&
               platformWheelCode.Contains("CreateRingSegment") &&
               platformWheelCode.Contains("TimeSpan.FromSeconds(5)") &&
               platformWheelCode.Contains("MutedSegmentBrush") &&
               platformWheelCode.Contains("AnimateHoverExpansion(9d)") &&
               platformWheelCode.Contains("outerRadius * 0.7465d") &&
               platformWheelCode.Contains("HighlightedPlatformProperty") &&
               platformWheelCode.Contains("HoveredPlatformChanged") &&
               platformWheelCode.Contains("rotationTimer.Stop()") &&
               ExophasePlatformWheel.FindSegmentAtAngle(hoverSegments, 45d) == 0 &&
               ExophasePlatformWheel.FindSegmentAtAngle(hoverSegments, 180d) == 1 &&
               ExophasePlatformWheel.FindSegmentAtAngle(hoverSegments, 315d) == 2,
            "The Exophase statistics card should pair an interactive rotating platform wheel with global totals and a natural-height, two-column legend without nested scrolling or wasted card width.");
        var platformRatios = ExophasePlatformTimeline.CalculateSegmentRatios(
            new ulong[] { 3600, 7200, 3600 });
        Expect(platformRatios.Length == 3 &&
               Math.Abs(platformRatios[0] - 0.25) < 0.0001 &&
               Math.Abs(platformRatios[1] - 0.5) < 0.0001 &&
               Math.Abs(platformRatios[2] - 0.25) < 0.0001 &&
                   ExophasePlatformTimeline.CalculateSegmentRatios(new ulong[] { 0, 0 }).Length == 0,
            "The global-page bar should divide its full width in direct proportion to positive platform play time.");
        Expect(ExophasePlatformTimeline.CalculateTrophyProgressRatio(23, 64) > 0.359 &&
               ExophasePlatformTimeline.CalculateTrophyProgressRatio(23, 64) < 0.360 &&
               ExophasePlatformTimeline.CalculateTrophyProgressRatio(0, 64) == 0d &&
               ExophasePlatformTimeline.CalculateTrophyProgressRatio(80, 64) == 1d &&
               platformTimelineCode.Contains("TrophyProgressPen") &&
               platformTimelineCode.Contains("new DashStyle(new[] { 3d, 2d }, 0)") &&
               platformTimelineCode.Contains("track.Width * ratio") &&
               platformTimelineCode.Contains("track.Left - TrophyOutlineOffset") &&
               platformTimelineCode.Contains("track.Right + TrophyOutlineOffset") &&
               platformTimelineCode.Contains("if (ratio >= 1d)") &&
               platformTimelineCode.Contains("CompletedTrophyProgressPen") &&
               platformTimelineCode.Contains("TrophyProgressRatio >= 1d") &&
               !platformTimelineCode.Contains("CompletedTrophyGlowBrush") &&
               trophyRows[1].IsTrophyComplete &&
               !trophyRows[0].IsTrophyComplete,
            "The Exophase bar should overlay trophy completion as a dense, evenly offset dashed frame that stays open on the right until 100 percent.");
        Expect(ExophasePlatformTimeline.FormatPercentage(0.25) == "(25%)" &&
               ExophasePlatformTimeline.FormatPercentage(0.526) == "(53%)" &&
               ExophasePlatformTimeline.FormatPercentage(2) == "(100%)" &&
               globalPageCode.Contains("IconGeometry = visual.IconGeometry") &&
               globalPageCode.Contains("UseOsirisLogo = visual.UseOsirisLogo") &&
               globalPageCode.Contains("CreateTagBrush(visual.Brush)") &&
               globalPageCode.Contains("visual.UseOsirisLogo") &&
               platformTimelineCode.Contains("OsirisTagForegroundBrush") &&
               platformTimelineCode.Contains("PushOpacityMask(OsirisLogoMaskBrush)") &&
               globalPageCode.Contains("timeline.TotalPlaytimeSeconds"),
            "Platform labels should show a percentage and reuse the card's monochrome platform marks inside brand-coloured tags.");
        Expect(ExophaseGlobalPageControl.FormatTrophyCount(0, 64) == "0/64" &&
               ExophaseGlobalPageControl.FormatTrophyCount(1, 64) == "1/64" &&
               ExophaseGlobalPageControl.FormatTrophyCount(23, 64) == "23/64" &&
               globalPageView.Contains("Setter Property=\"Stroke\" Value=\"#777A82\"") &&
               globalPageView.Contains("Binding IsTrophyComplete") &&
               globalPageView.Contains("CompletedTrophyBrush") &&
               !globalPageView.Contains("CompletedTrophyGlowBrush") &&
               globalPageView.Contains("FontSize=\"16\"") &&
               globalPageView.Contains("StrokeLineJoin=\"Round\""),
            "The Game column should show a dim trophy icon with Exophase's compact earned/total award count.");
        var selectedTrophyPlatform = ExophaseGlobalPageControl.SelectTrophyPlatform(
            new[]
            {
                new ExophaseResolvedPlatform
                {
                    Platform = "Steam",
                    EarnedAwards = 31,
                    TotalAwards = 79
                },
                new ExophaseResolvedPlatform
                {
                    Platform = "PlayStation 4",
                    EarnedAwards = 53,
                    TotalAwards = 78
                },
                new ExophaseResolvedPlatform
                {
                    Platform = "Osiris",
                    EarnedAwards = 99,
                    TotalAwards = 99,
                    IsBaseline = true
                }
            });
        Expect(selectedTrophyPlatform != null &&
               selectedTrophyPlatform.Platform == "PlayStation 4" &&
               ExophaseGlobalPageControl.FormatTrophyCount(
                   selectedTrophyPlatform.EarnedAwards,
                   selectedTrophyPlatform.TotalAwards) == "53/78" &&
               globalPageCode.Contains("SelectTrophyPlatform(editableActivity.Platforms)"),
            "Cross-platform trophy sets should use one representative Exophase platform instead of summing duplicate achievements.");
        Expect(platformTimelineCode.Contains("private const double TrackHeight = 14") &&
               platformTimelineCode.Contains("private const double SegmentGap = 6") &&
               platformTimelineCode.Contains("track.Width - totalGapWidth") &&
               !platformTimelineCode.Contains("new RectangleGeometry(track, radius, radius)") &&
               !platformTimelineCode.Contains("TrackOutlinePen") &&
               !platformTimelineCode.Contains("SegmentDividerPen") &&
               !platformTimelineCode.Contains("SegmentDividerOutlinePen"),
            "The Exophase platform bar should render sharp independent rectangles separated by real empty gaps.");

        var settingsSource = File.ReadAllText(Path.Combine(extensionRoot, "source", "ExophaseSettings.cs"));
        var settingsViewSource = File.ReadAllText(Path.Combine(extensionRoot, "source", "ExophaseSettingsView.xaml.cs"));
        var pluginSource = File.ReadAllText(Path.Combine(extensionRoot, "source", "ExophasePlugin.cs"));
        Expect(pluginSource.Contains("public static ExophasePlugin Current") &&
               pluginSource.Contains("GetEffectivePlaytimeForOsiris") &&
               pluginSource.Contains("settings.OverrideDisplayedPlaytime") &&
               pluginSource.Contains("resolvedActivity.CanDisplayTotal") &&
               pluginSource.Contains("return game.Playtime") &&
               pluginSource.Contains("return resolvedActivity.TotalPlaytimeSeconds"),
            "Exophase should expose its guarded effective playtime to optional peer extensions.");
        Expect(settingsSource.Contains("internal async Task AuthenticateAsync()") &&
               settingsSource.Contains("if (IsConnected)") &&
               settingsSource.Contains("await SynchronizeAsync();") &&
               settingsViewSource.Contains("await settings.AuthenticateAsync();") &&
               pluginSource.Contains("await settings.RefreshConnectionStatusAsync();") &&
               pluginSource.Contains("if (!settings.IsConnected)") &&
               pluginSource.Contains("await settings.SynchronizeAsync(false);"),
            "A successful sign-in and an authenticated blank-profile startup should synchronize activity automatically.");

        CheckActivityParser();
        CheckActivityCard(extensionRoot);
        CheckSafeImportContract(extensionRoot);
    }

    private static void CheckActivityParser()
    {
        const string json = @"{
  'success': true,
  'games': [
    {
      'master_id': 101,
      'environment': { 'name': 'PlayStation 5' },
      'playtimeUnits': { 'hours': 34, 'minutes': 30 },
      'earned_awards': 8,
      'total_awards': 12,
      'percent': 66.7,
      'lastplayed_utc': 1700000000,
      'meta': { 'title': 'The Last of Us™ Part I', 'platforms': [ { 'name': 'PlayStation 5' } ] }
    },
    {
      'master_id': 202,
      'environment': 'Steam',
      'playtimeUnits': { 'hours': 19 },
      'earned_awards': 4,
      'total_awards': 10,
      'meta': { 'title': 'The Last of Us Part I' }
    }
  ]
}";
        var wrapped = "<html><body><pre>" +
                      System.Net.WebUtility.HtmlEncode(json) +
                      "</pre></body></html>";
        var unwrapped = ExophaseActivityParser.UnwrapJsonPage(wrapped);
        var games = ExophaseActivityParser.ParseGames(JObject.Parse(unwrapped));
        Expect(games.Count == 2 &&
               games[0].PlaytimeSeconds == (34UL * 3600UL + 30UL * 60UL) &&
               games[0].Platform == "PlayStation 5" &&
               games[1].Platform == "Steam",
            "The extractor should parse platform-specific Exophase playtime records.");

        var aggregates = ExophaseActivityParser.AggregateGames(games);
        Expect(aggregates.Count == 1 &&
               aggregates[0].Platforms.Count == 2 &&
               aggregates[0].TotalPlaytimeSeconds == (53UL * 3600UL + 30UL * 60UL),
            "Equivalent platform titles should aggregate into one cross-platform game total.");
        Expect(ExophaseActivityParser.NormalizeTitle("Pokémon™: Édition") == "pokemonedition",
            "Title matching should normalize punctuation, trademarks, spacing, and accents.");
        Expect(ExophaseActivityParser.FindPlayerProfileId(
                   "<script>window.playerProfileId = 39458;</script>") == "39458" &&
               ExophaseActivityParser.FindProfileSlug(
                   "<a href=\"/user/TestAgent/\">Profile</a>") == "TestAgent",
            "The signed-in browser page should resolve the numeric profile identity.");
        Expect(ExophaseActivityParser.GetProfilePageUrl("Test Agent") ==
               "https://www.exophase.com/user/Test%20Agent/",
            "Usernames should be converted into safe Exophase profile URLs.");
    }

    private static void CheckSafeImportContract(string extensionRoot)
    {
        var pluginSource = File.ReadAllText(Path.Combine(extensionRoot, "source", "ExophasePlugin.cs"));
        var extractionSource = File.ReadAllText(Path.Combine(extensionRoot, "source", "ExophaseExtractionService.cs"));
        Expect(pluginSource.Contains("candidates.Count != 1") &&
               pluginSource.Contains("GetGameSettingsForOsiris") &&
               pluginSource.Contains("SearchGamesForOsiris") &&
               pluginSource.Contains("SaveGameSettingsForOsiris") &&
               pluginSource.Contains("matchedTitle") &&
               !pluginSource.Contains("Database.Games.Update") &&
               !pluginSource.Contains("game.Playtime ="),
            "Exophase should match unique titles and expose per-game settings without overwriting native playtime.");
        Expect(extractionSource.Contains("exophase-raw-latest.json") &&
               extractionSource.Contains("exophase-activity-latest.json") &&
               extractionSource.Contains("File.Replace") &&
               extractionSource.Contains("CreateOffscreenView") &&
               extractionSource.Contains("browser.GetCookies()") &&
               extractionSource.Contains("TryAddWithoutValidation(\"Cookie\"") &&
               extractionSource.Contains("navigator.userAgent") &&
               extractionSource.Contains("fetch(") &&
               extractionSource.Contains("credentials:'include'") &&
               extractionSource.Contains("EnsureExophasePageAsync") &&
               extractionSource.Contains("CanExecuteJavascriptInMainFrame") &&
               extractionSource.Contains("Task.Delay(500, cancellationToken)"),
            "The extractor should atomically cache snapshots and reuse the verified browser session for API requests and verification retries.");
    }

    private static void CheckActivityCard(string extensionRoot)
    {
        Expect(ExophaseActivityControl.FormatCompactPlaytime(0) == "0m" &&
               ExophaseActivityControl.FormatCompactPlaytime(60) == "1m" &&
               ExophaseActivityControl.FormatCompactPlaytime(2UL * 3600UL + 3UL * 60UL) ==
                   "2h 3m" &&
               ExophaseActivityControl.FormatCompactPlaytime(54UL * 3600UL) == "54h",
            "The details card should use the requested compact hours-and-minutes format.");
        Expect(ExophaseActivityControl.FormatDisplayPlaytime(60) == "1 Minute" &&
               ExophaseActivityControl.FormatDisplayPlaytime(2UL * 3600UL) == "2 Hours" &&
               ExophaseActivityControl.FormatDisplayPlaytime(2UL * 3600UL + 3UL * 60UL) == "2h 3m",
            "The optional total should use a compact readable display value.");
        Expect(ExophaseActivityControl.GetSourceTag(new ExophaseResolvedPlatform
               {
                   IsBaseline = true,
                   IsManual = true
               }) == "Local" &&
               ExophaseActivityControl.GetSourceTag(new ExophaseResolvedPlatform
               {
                   IsManual = true
               }) == "Manual" &&
               ExophaseActivityControl.GetSourceTag(new ExophaseResolvedPlatform()) == "Exophase",
            "Platform rows should distinguish native, manual, and synchronized play-time provenance.");
        Expect(!ExophaseActivityControl.HasDisplayableActivity(null) &&
               !ExophaseActivityControl.HasDisplayableActivity(new ExophaseResolvedActivity
               {
                   Enabled = true
               }) &&
               !ExophaseActivityControl.HasDisplayableActivity(new ExophaseResolvedActivity
               {
                   Enabled = false,
                   Platforms = new List<ExophaseResolvedPlatform>
                   {
                       new ExophaseResolvedPlatform { Platform = "Steam" }
                   }
               }) &&
               ExophaseActivityControl.HasDisplayableActivity(new ExophaseResolvedActivity
               {
                   Enabled = true,
                   Platforms = new List<ExophaseResolvedPlatform>
                   {
                       new ExophaseResolvedPlatform { Platform = "Steam" }
                   }
               }),
            "The details card should remain hidden until an enabled game has synchronized or manually added platform activity.");

        var steamVisual = ExophasePlatformVisualCatalog.Resolve("Steam");
        var playStationVisual = ExophasePlatformVisualCatalog.Resolve("PS4");
        var xboxVisual = ExophasePlatformVisualCatalog.Resolve("Xbox Series, Xbox One, Windows");
        var windowsVisual = ExophasePlatformVisualCatalog.Resolve("Windows");
        var switchVisual = ExophasePlatformVisualCatalog.Resolve("Nintendo Switch");
        var osirisVisual = ExophasePlatformVisualCatalog.Resolve("Osiris");
        Expect(steamVisual.DisplayName == "Steam" &&
               playStationVisual.DisplayName == "PlayStation 4" &&
               xboxVisual.DisplayName == "Xbox" &&
               steamVisual.ColorHex == "#66C0F4" &&
               playStationVisual.ColorHex == "#0070D1" &&
               xboxVisual.ColorHex == "#107C10" &&
               windowsVisual.DisplayName == "Windows" &&
               switchVisual.DisplayName == "Nintendo Switch" &&
               switchVisual.ColorHex == "#E60012" &&
               osirisVisual.DisplayName == "Osiris" &&
               osirisVisual.ColorHex == "#F0F1F7" &&
               steamVisual.IconGeometry != null &&
               playStationVisual.IconGeometry != null &&
               xboxVisual.IconGeometry != null &&
               windowsVisual.IconGeometry != null &&
               switchVisual.IconGeometry != null,
            "Known Exophase platforms should resolve to distinct brand colours, names, and vector marks.");
        Expect(windowsVisual.IconGeometry.Bounds.Width == 24d &&
               switchVisual.IconGeometry.Bounds.Width == 24d,
            "Windows and Nintendo Switch should use their proper monochrome brand marks instead of the generic four-square fallback.");

        var controlView = File.ReadAllText(Path.Combine(
            extensionRoot,
            "source",
            "ExophaseActivityControl.xaml"));
        var projectSource = File.ReadAllText(Path.Combine(
            extensionRoot,
            "source",
            "Osiris.Exophase.csproj"));
        var osirisLogoPath = Path.Combine(
            extensionRoot,
            "source",
            "Assets",
            "osiris-logo.png");
        var visualSource = File.ReadAllText(Path.Combine(
            extensionRoot,
            "source",
            "ExophasePlatformVisuals.cs"));
        var activityControlSource = File.ReadAllText(Path.Combine(
            extensionRoot,
            "source",
            "ExophaseActivityControl.xaml.cs"));
        var pluginSource = File.ReadAllText(Path.Combine(extensionRoot, "source", "ExophasePlugin.cs"));
        Expect(controlView.Contains("x:Name=\"ActivityPanel\"") &&
               controlView.Contains("ItemsSource=\"{Binding Platforms}\"") &&
               controlView.Contains("Background=\"#070707\"") &&
               !controlView.Contains("Background=\"#000000\"") &&
               controlView.Contains("x:Key=\"PlatformRow\"") &&
               controlView.Contains("Padding\" Value=\"20,15\"") &&
               controlView.Contains("Width\" Value=\"60\"") &&
               controlView.Contains("Height\" Value=\"60\"") &&
               controlView.Contains("BorderBrush\" Value=\"#AFAFAF\"") &&
               controlView.Contains("<ColumnDefinition Width=\"110\" />") &&
               controlView.Contains("Data=\"{Binding IconGeometry}\"") &&
               controlView.Contains("Binding UseOsirisLogo") &&
               controlView.Contains("/Osiris.Exophase;component/Assets/osiris-logo.png") &&
               controlView.Contains("Text=\"{Binding Platform}\"") &&
               controlView.Contains("Text=\"{Binding SourceTag}\"") &&
               controlView.Contains("Foreground=\"#5F6269\"") &&
               controlView.Contains("Text=\"{Binding PlaytimeText}\"") &&
               controlView.Contains("Binding IsLast") &&
               !controlView.Contains("TrophyText") &&
               !controlView.Contains("PlatformBrush") &&
               !controlView.Contains("UniformGrid") &&
               !controlView.Contains("PLAY TIME DISTRIBUTION") &&
               !controlView.Contains("PLATFORM BREAKDOWN") &&
               !controlView.Contains("TOTAL HOURS") &&
               !visualSource.Contains("ExophaseDonutChart") &&
               osirisVisual.UseOsirisLogo &&
               osirisVisual.IconGeometry == null &&
               !steamVisual.UseOsirisLogo &&
               activityControlSource.Contains("UseOsirisLogo = visual.UseOsirisLogo") &&
               activityControlSource.Contains("public bool UseOsirisLogo") &&
               activityControlSource.Contains("public string SourceTag") &&
               activityControlSource.Contains("SourceTag = GetSourceTag(platform)") &&
               activityControlSource.Contains("public bool HasOverflow") &&
               activityControlSource.Contains("HasOverflow = rows.Count > 2") &&
               activityControlSource.Contains("IsCardVisible = rows.Count > 0") &&
               !activityControlSource.Contains("IsCardVisible = resolvedActivity.Enabled") &&
               activityControlSource.Contains("!editableActivity.UsesManualMatch") &&
               projectSource.Contains("Resource Include=\"Assets\\osiris-logo.png\"") &&
               File.Exists(osirisLogoPath) &&
               new FileInfo(osirisLogoPath).Length > 0 &&
               pluginSource.Contains("SourceName = ExtensionSource") &&
               pluginSource.Contains("ActivityControlName = \"ActivityViewControl\"") &&
               pluginSource.Contains("activityResolver") &&
               pluginSource.Contains("gameSettingsStore"),
            "Exophase should clone the HLTB row geometry, match its row gaps to the host card body, and use the official Osiris logo for the Osiris platform row.");

        var temporaryRoot = Path.Combine(Path.GetTempPath(), "osiris-exophase-card-" + Guid.NewGuid());
        Directory.CreateDirectory(temporaryRoot);
        try
        {
            var snapshot = new ExophaseActivitySnapshot
            {
                PlayerProfileId = "39458",
                FetchedUtc = new DateTime(2026, 9, 12, 12, 0, 0, DateTimeKind.Utc),
                Games = new List<ExophaseGameActivity>
                {
                    new ExophaseGameActivity
                    {
                        Title = "The Last of Us™ Part I",
                        Platform = "PlayStation 5",
                        PlaytimeSeconds = 34UL * 3600UL,
                        EarnedAwards = 8,
                        TotalAwards = 12
                    },
                    new ExophaseGameActivity
                    {
                        Title = "The Last of Us Part I",
                        Platform = "Steam",
                        PlaytimeSeconds = 19UL * 3600UL,
                        EarnedAwards = 4,
                        TotalAwards = 10
                    },
                    new ExophaseGameActivity
                    {
                        Title = "Only Steam",
                        Platform = "Steam",
                        PlaytimeSeconds = 12UL * 3600UL
                    },
                    new ExophaseGameActivity
                    {
                        Title = "Assassin's Creed IV: Black Flag",
                        Platform = "PlayStation 4",
                        PlaytimeSeconds = 40UL * 3600UL
                    },
                    new ExophaseGameActivity
                    {
                        Title = "The Witcher 3: Wild Hunt - Game of the Year Edition",
                        Platform = "PlayStation 4",
                        PlaytimeSeconds = 70UL * 3600UL,
                        EarnedAwards = 20,
                        TotalAwards = 50
                    },
                    new ExophaseGameActivity
                    {
                        Title = "The Witcher 3: Wild Hunt - Complete Edition",
                        Platform = "Steam",
                        PlaytimeSeconds = 25UL * 3600UL,
                        EarnedAwards = 10,
                        TotalAwards = 30
                    },
                    new ExophaseGameActivity
                    {
                        Title = "The Witcher 3: Wild Hunt - Complete Edition",
                        Platform = "PlayStation 4",
                        PlaytimeSeconds = 5UL * 3600UL,
                        EarnedAwards = 2,
                        TotalAwards = 10
                    }
                }
            };
            File.WriteAllText(
                Path.Combine(temporaryRoot, "exophase-activity-latest.json"),
                JsonConvert.SerializeObject(snapshot));
            var store = new ExophaseActivityStore(temporaryRoot);
            var snapshotInfo = store.GetSnapshotInfo();
            Expect(snapshotInfo.HasSnapshot &&
                   !snapshotInfo.SnapshotIsInvalid &&
                   snapshotInfo.FetchedUtc == snapshot.FetchedUtc,
                "The activity store should expose the persisted database timestamp without requiring a game lookup.");
            var lookup = store.FindGame("The Last of Us: Part I");
            Expect(lookup.HasSnapshot &&
                   !lookup.SnapshotIsInvalid &&
                   lookup.Game != null &&
                   lookup.Game.Platforms.Count == 2 &&
                   lookup.Game.TotalPlaytimeSeconds == 53UL * 3600UL,
                "The details-card store should read and aggregate the latest private Exophase snapshot.");
            var searchMatches = store.SearchGames("last of us", 10);
            Expect(searchMatches.Count == 1 &&
                   searchMatches[0].MatchKey ==
                       ExophaseActivityParser.NormalizeTitle("The Last of Us Part I") &&
                   searchMatches[0].Platforms.Count == 2 &&
                   store.SearchGames(string.Empty, 10).Count == 0,
                "Game Edit should search the synchronized Exophase library without changing its snapshot.");
            var fuzzyMatches = store.SearchGames("Assassin's Creed Black Flag Resynced", 10);
            Expect(fuzzyMatches.Count == 1 &&
                   fuzzyMatches[0].Title == "Assassin's Creed IV: Black Flag",
                "Game Edit search should tolerate platform, edition, and release-title differences.");

            var changed = false;
            store.SnapshotChanged += () => changed = true;
            store.Update(snapshot);
            Expect(changed,
                "A completed Exophase synchronization should refresh any visible details card.");

            var gameSettings = new ExophaseGameSettingsStore(temporaryRoot);
            var resolver = new ExophaseActivityResolver(store, gameSettings);
            var localGame = new Game
            {
                Id = Guid.NewGuid(),
                Name = "The Last of Us Part I",
                PluginId = Guid.Empty,
                Playtime = 45UL * 3600UL
            };
            var localTotal = resolver.Resolve(localGame, true);
            Expect(localTotal.PositivePlatformCount == 3 &&
                   localTotal.TotalPlaytimeSeconds == 98UL * 3600UL &&
                   localTotal.Platforms.Any(item => item.Platform == "Osiris"),
                "A local Osiris baseline should remain a distinct platform in the cross-platform total.");

            var differentlyNamedGame = new Game
            {
                Id = Guid.NewGuid(),
                Name = "The Last of Us Remake - Local Copy",
                PluginId = Guid.Empty,
                Playtime = 45UL * 3600UL
            };
            gameSettings.Save(differentlyNamedGame.Id, new ExophaseGameSettings
            {
                MatchedTitle = "The Last of Us Part I"
            });
            var manuallyMatchedTotal = resolver.Resolve(differentlyNamedGame, true);
            Expect(manuallyMatchedTotal.UsesManualMatch &&
                   manuallyMatchedTotal.MatchedTitle == "The Last of Us Part I" &&
                   manuallyMatchedTotal.MatchedTitles.Count == 1 &&
                   manuallyMatchedTotal.PositivePlatformCount == 3 &&
                   manuallyMatchedTotal.TotalPlaytimeSeconds == 98UL * 3600UL,
                "A saved Exophase title selection should resolve platform time for a differently named Osiris game.");

            var multiEditionGame = new Game
            {
                Id = Guid.NewGuid(),
                Name = "The Witcher 3",
                PluginId = Guid.Empty,
                Playtime = 8UL * 3600UL
            };
            gameSettings.Save(multiEditionGame.Id, new ExophaseGameSettings
            {
                MatchedTitles = new List<string>
                {
                    "The Witcher 3: Wild Hunt - Game of the Year Edition",
                    "The Witcher 3: Wild Hunt - Complete Edition"
                }
            });
            var multiEditionTotal = resolver.Resolve(multiEditionGame, true);
            Expect(multiEditionTotal.UsesManualMatch &&
                   multiEditionTotal.MatchedTitles.Count == 2 &&
                   multiEditionTotal.PositivePlatformCount == 3 &&
                   multiEditionTotal.TotalPlaytimeSeconds == 108UL * 3600UL &&
                   multiEditionTotal.Platforms.Single(item => item.Platform == "PlayStation 4").PlaytimeSeconds ==
                       75UL * 3600UL &&
                   multiEditionTotal.Platforms.Single(item => item.Platform == "PlayStation 4").EarnedAwards == 20 &&
                   multiEditionTotal.Platforms.Single(item => item.Platform == "PlayStation 4").TotalAwards == 50 &&
                   multiEditionTotal.Platforms.Single(item => item.Platform == "Steam").PlaytimeSeconds ==
                       25UL * 3600UL,
                "Multiple Exophase editions should combine platform playtime without duplicating one platform's trophy set or losing the local Osiris baseline.");

            var legacyImportedGame = new Game
            {
                Id = Guid.NewGuid(),
                Name = "The Last of Us Part I",
                PluginId = Guid.Empty,
                Playtime = 53UL * 3600UL
            };
            var legacyImportedTotal = resolver.Resolve(legacyImportedGame, true);
            Expect(legacyImportedTotal.PositivePlatformCount == 2 &&
                   legacyImportedTotal.TotalPlaytimeSeconds == 53UL * 3600UL &&
                   legacyImportedTotal.Platforms.All(item => item.Platform != "Osiris"),
                "A native value matching the old imported aggregate should not be counted again as Osiris time.");

            var steamGame = new Game
            {
                Id = Guid.NewGuid(),
                Name = "The Last of Us Part I",
                PluginId = new Guid("cb91dfc9-b977-43bf-8e70-55f46e410fab"),
                Playtime = 18UL * 3600UL
            };
            gameSettings.Save(steamGame.Id, new ExophaseGameSettings
            {
                Platforms = new List<ExophasePlatformSetting>
                {
                    new ExophasePlatformSetting
                    {
                        Id = "manual:steam",
                        Platform = "Steam",
                        PlaytimeSeconds = 100UL * 3600UL,
                        IsManual = true
                    }
                }
            });
            var steamTotal = resolver.Resolve(steamGame, true);
            Expect(steamTotal.PositivePlatformCount == 2 &&
                   steamTotal.TotalPlaytimeSeconds == 52UL * 3600UL &&
                   steamTotal.CanDisplayTotal &&
                   steamTotal.Platforms.Single(item => item.Platform == "Steam").PlaytimeSeconds == 18UL * 3600UL,
                "The current integrated-library source should use native Time Played and ignore matching Exophase/manual time.");
            var unplayedSteamGame = new Game
            {
                Id = Guid.NewGuid(),
                Name = "The Last of Us Part I",
                PluginId = new Guid("cb91dfc9-b977-43bf-8e70-55f46e410fab"),
                Playtime = 0UL
            };
            var unplayedSteamTotal = resolver.Resolve(unplayedSteamGame, true);
            Expect(unplayedSteamTotal.PositivePlatformCount == 1 &&
                   unplayedSteamTotal.CanDisplayTotal &&
                   unplayedSteamTotal.TotalPlaytimeSeconds == 34UL * 3600UL &&
                   unplayedSteamTotal.Platforms.Single(item => item.Platform == "Steam").PlaytimeSeconds == 0UL,
                "A zero-hour integrated source should still unlock the total when another platform has positive time.");
            var steamOnlyGame = new Game
            {
                Id = Guid.NewGuid(),
                Name = "Only Steam",
                PluginId = new Guid("cb91dfc9-b977-43bf-8e70-55f46e410fab"),
                Playtime = 5UL * 3600UL
            };
            var steamOnly = resolver.Resolve(steamOnlyGame, true);
            Expect(steamOnly.PositivePlatformCount == 1 &&
                   steamOnly.TotalPlaytimeSeconds == 5UL * 3600UL &&
                   !steamOnly.CanDisplayTotal,
                "A game played on only its current library platform should keep native Time Played without an Exophase total.");
            var unplayedLocalGame = new Game
            {
                Id = Guid.NewGuid(),
                Name = "Only Steam",
                PluginId = Guid.Empty,
                Playtime = 0UL
            };
            var unplayedLocalTotal = resolver.Resolve(unplayedLocalGame, true);
            Expect(unplayedLocalTotal.PositivePlatformCount == 1 &&
                   unplayedLocalTotal.CanDisplayTotal &&
                   unplayedLocalTotal.TotalPlaytimeSeconds == 12UL * 3600UL &&
                   unplayedLocalTotal.Platforms.Single(item => item.Platform == "Osiris").PlaytimeSeconds == 0UL,
                "A zero-hour local Osiris source should still unlock the total when an external platform has positive time.");
            Expect(ExophaseActivityResolver.NormalizePlatformKey("Xbox Series X|S") ==
                   ExophaseActivityResolver.NormalizePlatformKey("Xbox"),
                "Platform aliases should resolve to one source bucket before totals are calculated.");

            gameSettings.Save(localGame.Id, new ExophaseGameSettings
            {
                Enabled = false,
                MatchedTitle = "The Last of Us Part I",
                Platforms = new List<ExophasePlatformSetting>
                {
                    new ExophasePlatformSetting
                    {
                        Id = "manual:nintendo",
                        Platform = "Nintendo Switch",
                        PlaytimeSeconds = 2UL * 3600UL,
                        IsManual = true
                    }
                }
            });
            var reloadedSettings = new ExophaseGameSettingsStore(temporaryRoot).Load(localGame.Id);
            Expect(!reloadedSettings.Enabled &&
                   reloadedSettings.MatchedTitle == "The Last of Us Part I" &&
                   reloadedSettings.MatchedTitles.SequenceEqual(new[] { "The Last of Us Part I" }) &&
                   reloadedSettings.Platforms.Count == 1 &&
                   reloadedSettings.Platforms[0].Platform == "Nintendo Switch",
                "Per-game enable state, explicit match, and manual platform time should survive a restart.");
        }
        finally
        {
            Directory.Delete(temporaryRoot, true);
        }
    }

    private static void Expect(bool condition, string message)
    {
        checks++;
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
