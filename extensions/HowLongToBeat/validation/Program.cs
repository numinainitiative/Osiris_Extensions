using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Osiris.Extensions.HowLongToBeat;

internal static class Program
{
    private static int checks;

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            RunLocalChecks();
            if (args.Any(argument => string.Equals(argument, "--live", StringComparison.OrdinalIgnoreCase)))
            {
                RunLiveCheck();
            }

            Console.WriteLine($"PASS checks={checks}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL " + exception);
            return 1;
        }
    }

    private static void RunLocalChecks()
    {
        Expect(CompletionTimeFormatting.Format(94005) == "26 Hours", "Main-story hours should round correctly.");
        Expect(CompletionTimeFormatting.Format(3599) == "1 Hour", "Sub-hour estimates should remain readable.");
        Expect(CompletionTimeFormatting.Format(0) == "—", "Missing estimates should use an em dash.");
        Expect(Math.Abs(CompletionTimeFormatting.ProgressPercent(25UL * 3600, 50L * 3600) - 50d) < 0.001,
            "Played-time progress should compare local playtime with the selected completion estimate.");
        Expect(CompletionTimeFormatting.ProgressPercent(75UL * 3600, 50L * 3600) == 100d,
            "Played-time progress should clamp completed estimates to a full bar.");
        Expect(CompletionTimeFormatting.ProgressPercent(25UL * 3600, 0) == 0d,
            "Missing completion estimates should leave the progress track empty.");
        var extensionRoot = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", ".."));
        var cardView = File.ReadAllText(Path.Combine(extensionRoot, "source", "CompletionTimesControl.xaml"));
        var globalPageView = File.ReadAllText(Path.Combine(
            extensionRoot,
            "source",
            "HowLongToBeatGlobalPageControl.xaml"));
        var globalPageCode = File.ReadAllText(Path.Combine(
            extensionRoot,
            "source",
            "HowLongToBeatGlobalPageControl.cs"));
        var settingsCode = File.ReadAllText(Path.Combine(
            extensionRoot,
            "source",
            "HowLongToBeatSettings.cs"));
        var footerProgressCode = File.ReadAllText(Path.Combine(
            extensionRoot,
            "source",
            "OsirisFooterUpdateProgress.cs"));
        Expect(cardView.Contains("<Setter Property=\"CornerRadius\" Value=\"0\" />") &&
               cardView.Contains("<Setter Property=\"Height\" Value=\"10\" />") &&
               cardView.Contains("<ColumnDefinition Width=\"170\" />") &&
               cardView.Contains("<Setter Property=\"Background\" Value=\"Transparent\" />") &&
               cardView.Contains("Geometry=\"M0,10 L4,10 10,0 6,0 Z\"") &&
               cardView.Contains("RenderOptions.EdgeMode=\"Aliased\"") &&
               cardView.Contains("<Setter Property=\"FontSize\" Value=\"18\" />") &&
               cardView.Contains("<Setter Property=\"FontSize\" Value=\"15\" />") &&
               cardView.Contains("Background=\"#F4F5F8\"") &&
               cardView.Contains("CornerRadius=\"5,0,0,5\"") &&
               cardView.Contains("<Trigger Property=\"Value\" Value=\"100\">") &&
               cardView.Contains("TargetName=\"PART_Indicator\"") &&
               cardView.Contains("BorderBrush=\"#626262\"") &&
               !cardView.Contains("#101114") &&
               !cardView.Contains("#3F4249") &&
               !cardView.Contains("#454850"),
            "The card should use transparent icon frames and a compact neutral-grey striped track with a flat partial-fill edge and sealed full state.");
        Expect(cardView.Contains("<StackPanel x:Name=\"ResultPanel\" Background=\"#000000\">") &&
               cardView.Contains("x:Name=\"MainStoryRow\"") &&
               cardView.Contains("x:Name=\"MainExtraRow\"") &&
               cardView.Split(new[] { "Background=\"#090909\"" }, StringSplitOptions.None).Length - 1 == 2 &&
               cardView.Contains("x:Name=\"CompletionistRow\" Style=\"{StaticResource TimeRow}\" Margin=\"0\"") &&
               cardView.Contains("<Setter Property=\"Background\" Value=\"#101010\" />"),
            "The first two rows should match the Overview background, the separators should be black, and Completionist should retain its existing background.");
        var bookIcon = File.ReadAllText(Path.Combine(extensionRoot, "source", "Icons", "book-open-check.svg"));
        var pickaxeIcon = File.ReadAllText(Path.Combine(extensionRoot, "source", "Icons", "pickaxe.svg"));
        var trophyIcon = File.ReadAllText(Path.Combine(extensionRoot, "source", "Icons", "trophy.svg"));
        Expect(bookIcon.Contains("lucide lucide-book-open-check") &&
               pickaxeIcon.Contains("lucide lucide-pickaxe") &&
               trophyIcon.Contains("lucide lucide-trophy") &&
               cardView.Contains("Icons/book-open-check.svg") &&
               cardView.Contains("Icons/pickaxe.svg") &&
               cardView.Contains("Icons/trophy.svg"),
            "The card should retain and render geometry from the three supplied original SVG icons.");
        Expect(CompletionTimeMatching.Normalize("Pokémon: Édition!") == "pokemon edition", "Title normalization should remove marks and punctuation.");

        var candidates = new List<SearchCandidate>
        {
            new SearchCandidate
            {
                GameId = 2,
                GameName = "Cyberpunk 2077: Phantom Liberty",
                GameType = "dlc",
                ReleaseWorld = 2023,
                MainStorySeconds = 81000
            },
            new SearchCandidate
            {
                GameId = 1,
                GameName = "Cyberpunk 2077",
                GameType = "game",
                ReleaseWorld = 2020,
                MainStorySeconds = 94005,
                MainExtraSeconds = 227642,
                CompletionistSeconds = 392193
            }
        };
        var selected = CompletionTimeMatching.SelectBest("Cyberpunk 2077", 2020, candidates);
        Expect(selected?.GameId == 1, "Exact base-game result should beat similarly named DLC.");

        var payload = HowLongToBeatClient.BuildPayload("Cyberpunk 2077", "proof", "value");
        Expect(payload.Value<string>("searchType") == "games", "Search payload should target games.");
        Expect(payload.Value<string>("proof") == "value", "Search payload should contain the current proof value.");
        Expect(payload["searchTerms"].Count() == 2, "Search title should be tokenized.");

        var settings = new HowLongToBeatSettings();
        Expect(settings.TimeProfile == CompletionTimeProfiles.Average,
            "Average should be the default completion-time profile.");
        Expect(settings.ShowMainStory && settings.ShowMainExtras && settings.ShowCompletionist,
            "All three completion estimates should be visible by default.");
        Expect(settings.CanUpdateDatabase,
            "The database update action should be available while idle.");
        settings.BeginEdit();
        settings.TimeProfile = CompletionTimeProfiles.Leisure;
        settings.ShowMainExtras = false;
        List<string> settingsErrors;
        Expect(settings.VerifySettings(out settingsErrors), "Completion-time display settings should validate.");
        Expect(settingsErrors != null && settingsErrors.Count == 0, "Valid completion-time settings should not report errors.");
        settings.CancelEdit();
        Expect(settings.TimeProfile == CompletionTimeProfiles.Average && settings.ShowMainExtras,
            "Cancelling settings should restore the original profile and visible fields.");
        settings.EndEdit();

        var settingsView = new HowLongToBeatSettingsView();
        var settingsTabs = settingsView.Content as TabControl;
        var general = settingsTabs?.Items.Cast<TabItem>().FirstOrDefault();
        var dangerZone = settingsTabs?.Items.Cast<TabItem>().LastOrDefault();
        Expect(settingsTabs != null && settingsTabs.Items.Count == 2,
            "HowLongToBeat should expose General and Danger Zone settings categories.");
        Expect(string.Equals(general?.Header as string, "General", StringComparison.Ordinal),
            "General should be the first HowLongToBeat settings category.");
        Expect(string.Equals(dangerZone?.Header as string, "Danger Zone", StringComparison.Ordinal),
            "Danger Zone should remain the last HowLongToBeat settings category.");
        Expect(settingsView.FindName("UpdateDatabaseButton") is Button,
            "General settings should expose the Update Database action.");
        Expect(settingsView.FindName("DatabaseUpdateStatus") is TextBlock,
            "General settings should expose database update progress and results.");
        Expect(settingsCode.Contains("OsirisFooterUpdateProgress.TryStart") &&
               settingsCode.Contains("footerProgress?.CancellationToken") &&
               settingsCode.Contains("footerProgress?.Report") &&
               settingsCode.Contains("footerProgress?.Complete(\"All tasks are now completed\")") &&
               settingsCode.Contains("footerProgress?.Cancel()") &&
               footerProgressCode.Contains("OsirisTheme.OsirisUpdateActions") &&
               footerProgressCode.Contains("BeginFooterUpdateProcess") &&
               footerProgressCode.Contains("ShowFooterUpdateProgress") &&
               footerProgressCode.Contains("CompleteFooterUpdateProcess") &&
               footerProgressCode.Contains("ShowFooterUpdateCancelled") &&
               !footerProgressCode.Contains("ActivateGlobalProgress"),
            "Database updates should use Osiris's cancellable bottom-panel progress surface instead of a modal dialog.");

        var globalPage = new HowLongToBeatGlobalPageSidebarItem();
        var globalView = globalPage.Opened?.Invoke() as HowLongToBeatGlobalPageControl;
        var globalViewLayout = globalView?.Content as Grid;
        var globalViewHeader = globalViewLayout?.Children
            .OfType<FrameworkElement>()
            .FirstOrDefault(element => Grid.GetRow(element) == 0);
        Expect(globalPage.OsirisGlobalPage &&
               globalPage.Type == Playnite.SDK.Plugins.SiderbarItemType.View &&
               string.Equals(globalPage.Title, "How Long To Beat", StringComparison.Ordinal) &&
               globalView != null,
            "HowLongToBeat should opt into the Osiris extension-page list with its own global view.");
        Expect(globalViewLayout != null &&
               globalViewLayout.RowDefinitions.Count == 2 &&
               globalViewLayout.RowDefinitions[0].Height.IsAbsolute &&
               Math.Abs(globalViewLayout.RowDefinitions[0].Height.Value - 76) < 0.01 &&
               Math.Abs(globalViewLayout.RowDefinitions[0].MinHeight - 76) < 0.01 &&
               Math.Abs(globalViewLayout.RowDefinitions[0].MaxHeight - 76) < 0.01,
            "The global page should reserve a fixed 76 px row above its independently sized content area.");
        Expect(globalViewHeader != null &&
               globalViewHeader.Visibility == Visibility.Visible &&
               Math.Abs(globalViewHeader.MinHeight - 76) < 0.01 &&
               Math.Abs(globalViewHeader.MaxHeight - 76) < 0.01,
            "The global page header should remain fixed and visible while the page is open.");
        var globalGamesGrid = globalView?.FindName("GamesGrid") as DataGrid;
        Expect(globalGamesGrid != null &&
               !globalGamesGrid.AutoGenerateColumns &&
               globalGamesGrid.Columns.Count == 3 &&
               globalGamesGrid.EnableRowVirtualization &&
               globalGamesGrid.EnableColumnVirtualization,
            "The global page should expose a virtualized three-column game library.");
        Expect(globalGamesGrid.Columns[0].Header as string == "ICON" &&
               globalGamesGrid.Columns[1].Header as string == "GAME" &&
               globalGamesGrid.Columns[2].Header as string == "HOW LONG TO BEAT",
            "The global library should contain only Icon, Game, and How Long To Beat columns.");
        var sortByCombo = globalView?.FindName("SortByCombo") as ComboBox;
        var showOnlyPlayedToggle = globalView?.FindName("ShowOnlyPlayedToggle") as CheckBox;
        var globalUpdateDatabaseButton = globalView?.FindName("UpdateDatabaseButton") as Button;
        var headerGutterFill = globalView?.FindName("HeaderGutterFill") as Border;
        Expect(sortByCombo != null &&
               sortByCombo.Items.Count == 3 &&
               string.Equals(((ComboBoxItem)sortByCombo.Items[0]).Content as string, "Title", StringComparison.Ordinal) &&
               string.Equals(((ComboBoxItem)sortByCombo.Items[1]).Content as string, "Time Played", StringComparison.Ordinal) &&
               string.Equals(((ComboBoxItem)sortByCombo.Items[2]).Content as string, "Longest Completion", StringComparison.Ordinal) &&
               showOnlyPlayedToggle != null,
            "The HLTB toolbar should expose Title, Time Played, and Longest Completion sorting beside a played-games switch.");
        Expect(globalUpdateDatabaseButton != null &&
               string.Equals(globalUpdateDatabaseButton.Content as string, "Update Database", StringComparison.Ordinal) &&
               !globalUpdateDatabaseButton.IsEnabled &&
               globalPageView.Contains("x:Key=\"OsirisActionButtonStyle\"") &&
               globalPageView.Contains("Style=\"{StaticResource OsirisActionButtonStyle}\"") &&
               globalPageView.Contains("<Setter Property=\"Background\" Value=\"#111111\" />") &&
               globalPageView.Contains("<Setter Property=\"BorderBrush\" Value=\"#252525\" />") &&
               globalPageView.Contains("CornerRadius=\"7\"") &&
               globalPageView.Contains("TargetName=\"ActionBackground\" Property=\"Background\" Value=\"#171717\"") &&
               globalPageView.Contains("TargetName=\"ActionBackground\" Property=\"Background\" Value=\"#1A1A1A\"") &&
               globalPageCode.Contains("await settings.UpdateDatabaseAsync()") &&
               globalPageCode.Contains("nameof(HowLongToBeatSettings.CanUpdateDatabase)"),
            "The global toolbar should reuse the exact Osiris action-button treatment and the settings database-update operation.");
        Expect(globalPageView.Contains("Margin=\"52,23,52,0\"") &&
               globalPageView.Contains("<RowDefinition Height=\"84\" />") &&
               globalPageView.Contains("Margin=\"28,0,28,28\"") &&
               globalPageView.Contains("CornerRadius=\"0\"") &&
               globalPageView.Contains("CanUserSortColumns=\"False\"") &&
               headerGutterFill != null &&
               globalPageCode.Contains("scrollBar?.ActualWidth ?? 0"),
            "The rectangular table should sit beneath a Library-aligned toolbar and close its live scrollbar header gutter.");
        var bindingRow = new HowLongToBeatGameRow(
            new Playnite.SDK.Models.Game
            {
                Id = Guid.Parse("8bf41f44-2c57-4b45-8d41-c7dcd341e407"),
                Name = "Binding Test",
                Playtime = 3600
            },
            null,
            "osiris.png",
            CompletionTimeProfiles.Average,
            10L * 3600,
            20L * 3600,
            30L * 3600);
        globalGamesGrid.ItemsSource = new[] { bindingRow };
        globalView.Measure(new Size(1200, 700));
        globalView.Arrange(new Rect(0, 0, 1200, 700));
        globalView.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.DataBind, new Action(() => { }));
        Expect(globalGamesGrid.Items.Count == 1 &&
               bindingRow.PlayedTimeText == "1 Hour" &&
               bindingRow.FallbackIconPath == "osiris.png" &&
               bindingRow.GameId == Guid.Parse("8bf41f44-2c57-4b45-8d41-c7dcd341e407") &&
               bindingRow.SourceGame != null,
            "The rendered grid row should bind read-only local playtime without a TwoWay binding fault.");
        var validationRows = globalView.GamesView.SourceCollection as
            System.Collections.ObjectModel.ObservableCollection<HowLongToBeatGameRow>;
        var alphaRow = new HowLongToBeatGameRow(
            new Playnite.SDK.Models.Game { Name = "Alpha", Playtime = 0 },
            null, "osiris.png", CompletionTimeProfiles.Average,
            10L * 3600, 15L * 3600, 20L * 3600);
        var betaRow = new HowLongToBeatGameRow(
            new Playnite.SDK.Models.Game { Name = "Beta", Playtime = 2UL * 3600 },
            null, "osiris.png", CompletionTimeProfiles.Average,
            5L * 3600, 8L * 3600, 10L * 3600);
        var gammaRow = new HowLongToBeatGameRow(
            new Playnite.SDK.Models.Game { Name = "Gamma", Playtime = 1UL * 3600 },
            null, "osiris.png", CompletionTimeProfiles.Average,
            10L * 3600, 20L * 3600, 30L * 3600);
        validationRows.Clear();
        validationRows.Add(alphaRow);
        validationRows.Add(betaRow);
        validationRows.Add(gammaRow);
        globalGamesGrid.ItemsSource = globalView.GamesView;
        sortByCombo.SelectedIndex = 1;
        var timePlayedOrder = globalView.GamesView.Cast<HowLongToBeatGameRow>().ToList();
        sortByCombo.SelectedIndex = 2;
        var longestOrder = globalView.GamesView.Cast<HowLongToBeatGameRow>().ToList();
        sortByCombo.SelectedIndex = 0;
        var titleOrder = globalView.GamesView.Cast<HowLongToBeatGameRow>().ToList();
        showOnlyPlayedToggle.IsChecked = true;
        var playedRows = globalView.GamesView.Cast<HowLongToBeatGameRow>().ToList();
        Expect(timePlayedOrder.SequenceEqual(new[] { betaRow, gammaRow, alphaRow }) &&
               longestOrder.SequenceEqual(new[] { gammaRow, alphaRow, betaRow }) &&
               titleOrder.SequenceEqual(new[] { alphaRow, betaRow, gammaRow }),
            "Every HLTB toolbar sort option should apply the expected deterministic row ordering.");
        Expect(playedRows.SequenceEqual(new[] { betaRow, gammaRow }) &&
               playedRows.All(row => row.PlayedSeconds > 0),
            "Show only played games should remove every zero-playtime row without changing stored data.");
        showOnlyPlayedToggle.IsChecked = false;
        Expect(globalPageView.Contains("PlayedSeconds=\"{Binding PlayedSeconds}\"") &&
               globalPageView.Contains("MainStorySeconds=\"{Binding MainStorySeconds}\"") &&
               globalPageView.Contains("MainExtraSeconds=\"{Binding MainExtraSeconds}\"") &&
               globalPageView.Contains("CompletionistSeconds=\"{Binding CompletionistSeconds}\"") &&
               globalPageView.Contains("VirtualizingPanel.VirtualizationMode=\"Recycling\""),
            "Each virtualized row should bind local playtime and all three stored completion milestones to one timeline.");
        Expect(globalPageView.Contains("<Setter Property=\"Height\" Value=\"94\" />") &&
               globalPageView.Contains("<Run Text=\"Time Played&#xA0;\"") &&
               globalPageView.Contains("Text=\"{Binding PlayedTimeText, Mode=OneWay}\""),
            "Taller global-page rows should show local played time beneath every game title.");
        Expect(globalPageView.Contains("Header=\"GAME\"") &&
               globalPageView.Contains("Width=\"270\"") &&
               globalPageView.Contains("MinWidth=\"185\""),
            "The Game column should leave more horizontal space for the HLTB timeline.");
        Expect(globalPageView.Contains("Style=\"{StaticResource GameTitleButton}\"") &&
               globalPageView.Contains("Click=\"OnGameTitleClick\"") &&
               globalPageCode.Contains("OsirisTheme.HeaderNavigation") &&
               globalPageCode.Contains("ExecuteSwitchDetailsCommand") &&
               globalPageCode.Contains("api.MainView.ActiveDesktopView = DesktopView.Details"),
            "Each game title should explicitly navigate to that game's Osiris Details page.");
        Expect(globalPageView.Contains("Source=\"{Binding FallbackIconPath}\"") &&
               globalPageView.Contains("<DataTrigger Binding=\"{Binding IconPath}\" Value=\"{x:Null}\">") &&
               !globalPageView.Contains("Text=\"{Binding Initial}\"") &&
               globalPageCode.Contains("Path.Combine(imageRoot, \"applogo.png\")"),
            "Only games without a local icon should render the real Osiris fallback mark.");
        var timelineCode = File.ReadAllText(Path.Combine(
            extensionRoot,
            "source",
            "HowLongToBeatTimeline.cs"));
        Expect(timelineCode.Contains("CreateVerticalPatternBrush") &&
               timelineCode.Contains("MainExtraPatternBrush = CreateDiagonalPatternBrush()") &&
               timelineCode.Contains("CompletionistPatternBrush = EmptyTrackBrush") &&
               timelineCode.Contains("Geometry.Parse(\"M0,10 L4,10 10,0 6,0 Z\")") &&
               timelineCode.Contains("DrawCompletionistStripes") &&
               timelineCode.Contains("x += CompletionistStripeSpacing") &&
               timelineCode.Contains("segment.Top - CompletionistStripeOverscan") &&
               timelineCode.Contains("segment.Bottom + CompletionistStripeOverscan") &&
               timelineCode.Contains("segment.Height + (CompletionistStripeOverscan * 2)") &&
               timelineCode.Contains("StartLineCap = PenLineCap.Flat") &&
               timelineCode.Contains("EndLineCap = PenLineCap.Flat") &&
               timelineCode.Contains("marker.Position >= track.Right - 0.5") &&
               timelineCode.Contains("DrawPatternSegments") &&
               timelineCode.Contains("var segmentLeft = markerIndex > 0") &&
               timelineCode.Contains("CreateText(marker.Title, 15, LabelBrush)") &&
               timelineCode.Contains("CreateText(CompletionTimeFormatting.Format(marker.Seconds), 16, ValueBrush)") &&
               timelineCode.Contains("private const double TrackTop = 49") &&
               timelineCode.Contains("new Point(left, 16)") &&
               timelineCode.Contains("new Point(left + title.Width + gap, 15)") &&
               globalPageView.Contains("TimeProfile=\"{Binding TimeProfile}\"") &&
               globalPageView.Contains("VerticalAlignment=\"Center\"") &&
               timelineCode.Contains("if (marker.Seconds <= 0)") &&
               timelineCode.Contains("OrderBy(marker => marker.SemanticIndex)") &&
               !timelineCode.Contains("LeaderPen") &&
               !timelineCode.Contains("PatternSwatchWidth") &&
               !timelineCode.Contains("CreateHorizontalPatternBrush") &&
               !timelineCode.Contains("CreateSparseDiagonalPatternBrush") &&
               !timelineCode.Contains("CreateDotPatternBrush"),
            "Completionist should use overscanned full-height diagonal strokes with sharp outline-clipped ends, while the timeline omits its terminal tick and unavailable labels and preserves semantic order.");
        var battlefieldRatios = HowLongToBeatTimeline.CalculateSemanticMarkerRatios(
            88L * 3600,
            530L * 3600,
            206L * 3600);
        Expect(battlefieldRatios[0] < battlefieldRatios[1] &&
               battlefieldRatios[1] < battlefieldRatios[2],
            "Out-of-order HLTB values should still display Main Story, Main + Extras, then Completionist.");
        var partialRatios = HowLongToBeatTimeline.CalculateSemanticMarkerRatios(
            9L * 3600,
            0,
            0);
        Expect(!double.IsNaN(partialRatios[0]) &&
               double.IsNaN(partialRatios[1]) &&
               double.IsNaN(partialRatios[2]),
            "Unavailable HLTB estimates should not receive invented marker positions or labels.");
        Expect(globalPageView.Contains("<Run Text=\"Time Played&#xA0;\"") &&
               globalPageView.Contains("Text=\"{Binding PlayedTimeText, Mode=OneWay}\"") &&
               globalPageView.Contains("FontSize=\"16\"") &&
               globalPageView.Contains("Foreground=\"#F0F1F7\""),
            "Time Played should share one baseline and the timeline label/value typography.");
        Expect(!globalPageCode.Contains("SearchAsync(") &&
               !globalPageCode.Contains("GetDetailsAsync("),
            "Opening and filtering the global page should never fetch or stream HowLongToBeat data.");

        const string detailPage = "<html><script id=\"__NEXT_DATA__\" type=\"application/json\">" +
            "{\"props\":{\"pageProps\":{\"game\":{\"data\":{\"game\":[{" +
            "\"game_id\":2127,\"game_name\":\"Cyberpunk 2077\"," +
            "\"comp_main\":94005,\"comp_plus\":227642,\"comp_100\":392193," +
            "\"comp_main_l\":65003,\"comp_main_avg\":98009,\"comp_main_med\":90000,\"comp_main_h\":156803," +
            "\"comp_plus_l\":136512,\"comp_plus_avg\":239248,\"comp_plus_med\":216000,\"comp_plus_h\":632212," +
            "\"comp_100_l\":278927,\"comp_100_avg\":413585,\"comp_100_med\":370800,\"comp_100_h\":1108697" +
            "}]}}}}}</script></html>";
        var detailed = HowLongToBeatClient.ParseDetailPage(detailPage, 2127);
        Expect(detailed.GetMainStorySeconds(CompletionTimeProfiles.Rushed) == 65003,
            "Rushed should use the low completion-time estimate.");
        Expect(detailed.GetMainExtraSeconds(CompletionTimeProfiles.Average) == 239248,
            "Average should use the mean completion-time estimate.");
        Expect(detailed.GetCompletionistSeconds(CompletionTimeProfiles.Median) == 370800,
            "Median should use the median completion-time estimate.");
        Expect(detailed.GetCompletionistSeconds(CompletionTimeProfiles.Leisure) == 1108697,
            "Leisure should use the high completion-time estimate.");
        var unchangedDetailed = detailed.Clone();
        Expect(detailed.HasSameTimesAs(unchangedDetailed),
            "Identical exact-match records should not be rewritten.");
        unchangedDetailed.MainStoryAverageSeconds++;
        Expect(!detailed.HasSameTimesAs(unchangedDetailed),
            "A changed completion estimate should be detected for database updating.");

        var temporarySettingsPath = Path.Combine(
            Path.GetTempPath(),
            "Osiris-HLTB-validation-" + Guid.NewGuid().ToString("N"));
        try
        {
            var gameId = Guid.NewGuid();
            var store = new CompletionTimeGameSettingsStore(temporarySettingsPath);
            var defaults = store.Load(gameId);
            Expect(defaults.Enabled && defaults.ManualResult == null,
                "New games should use automatic matching with HowLongToBeat enabled.");

            var cache = new CompletionTimeCache(temporarySettingsPath);
            cache.Store("Durable Game", 2020, new CompletionTimeResult
            {
                Found = true,
                RemoteGameId = 20,
                MatchedName = "Durable Game",
                MainStorySeconds = 36000,
                FetchedUtc = DateTime.UtcNow.AddYears(-10)
            });
            CompletionTimeResult durableResult;
            Expect(cache.TryGet("Durable Game", 2020, true, out durableResult) &&
                   durableResult?.MainStorySeconds == 36000,
                "Successful completion estimates should remain available without expiry.");

            cache.Store("Missing Game", 2020, new CompletionTimeResult
            {
                Found = false,
                FetchedUtc = DateTime.UtcNow.AddDays(-2)
            });
            CompletionTimeResult expiredMiss;
            Expect(!cache.TryGet("Missing Game", 2020, true, out expiredMiss),
                "Unsuccessful searches should still be retried after one day.");

            store.StoreFetchedResult(gameId, false, "Durable Game", 2020, durableResult, false);
            var embedded = new CompletionTimeGameSettingsStore(temporarySettingsPath).Load(gameId);
            Expect(embedded.AutomaticResult?.RemoteGameId == 20 &&
                   embedded.AutomaticResult.MainStorySeconds == 36000 &&
                   embedded.AutomaticGameName == "Durable Game" &&
                   embedded.AutomaticReleaseYear == 2020,
                "Automatic estimates should be embedded in the per-game extension record.");

            store.Save(gameId, new CompletionTimeGameSettings
            {
                Enabled = false,
                AutomaticResult = embedded.AutomaticResult,
                AutomaticGameName = embedded.AutomaticGameName,
                AutomaticReleaseYear = embedded.AutomaticReleaseYear,
                ManualResult = new CompletionTimeResult
                {
                    Found = true,
                    RemoteGameId = 1,
                    MatchedName = "Cyberpunk 2077",
                    MainStorySeconds = 94005,
                    MainExtraSeconds = 227642,
                    CompletionistSeconds = 392193,
                    FetchedUtc = DateTime.UtcNow
                }
            });
            var reloaded = new CompletionTimeGameSettingsStore(temporarySettingsPath).Load(gameId);
            Expect(!reloaded.Enabled && reloaded.ManualResult?.RemoteGameId == 1 &&
                   reloaded.AutomaticResult?.RemoteGameId == 20,
                "Per-game enable state and both exact matches should survive a restart.");
        }
        finally
        {
            if (Directory.Exists(temporarySettingsPath))
            {
                Directory.Delete(temporarySettingsPath, true);
            }
        }
    }

    private static void RunLiveCheck()
    {
        using (var client = new HowLongToBeatClient())
        {
            var result = client.SearchAsync("Cyberpunk 2077", 2020, CancellationToken.None).GetAwaiter().GetResult();
            Expect(result.Found, "Live search should find Cyberpunk 2077.");
            Expect(result.MainStorySeconds > 0, "Live search should return Main Story time.");
            Expect(result.MainExtraSeconds > 0, "Live search should return Main + Extras time.");
            Expect(result.CompletionistSeconds > 0, "Live search should return Completionist time.");
            Expect(result.HasDetailedProfiles, "Live search should return all detailed completion-time profiles.");
            Expect(result.HasProfile(CompletionTimeProfiles.Rushed), "Live search should return Rushed estimates.");
            Expect(result.HasProfile(CompletionTimeProfiles.Average), "Live search should return Average estimates.");
            Expect(result.HasProfile(CompletionTimeProfiles.Median), "Live search should return Median estimates.");
            Expect(result.HasProfile(CompletionTimeProfiles.Leisure), "Live search should return Leisure estimates.");
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
