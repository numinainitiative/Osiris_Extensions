using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;

namespace Osiris.Extensions.Stats
{
    public sealed partial class StatsGlobalPageControl : UserControl
    {
        private const double FixedHeaderHeight = 76;
        private const string NativeTopPanelTypeName =
            "Playnite.DesktopApp.Controls.Views.TopPanel";
        private const string ExophasePluginTypeName =
            "Osiris.Extensions.Exophase.ExophasePlugin";
        private const string HowLongToBeatPluginTypeName =
            "Osiris.Extensions.HowLongToBeat.HowLongToBeatPlugin";
        private const double LayoutGap = 16d;
        private const int LayoutColumnCount = 10;
        private const double LayoutColumnPitch = 166d;
        private const double LayoutRowPitch = 173d;

        private static readonly ILogger Logger = LogManager.GetLogger();
        private readonly IPlayniteAPI api;
        private readonly StatsSessionLedger sessionLedger;
        private readonly FrameworkElement fixedHeader;
        private readonly string layoutPath;
        private readonly string weeklyTrophyPath;
        private readonly List<FrameworkElement> layoutItems = new List<FrameworkElement>();
        private readonly List<Canvas> layoutCanvases = new List<Canvas>();
        private readonly Dictionary<FrameworkElement, Canvas> layoutCanvasByItem =
            new Dictionary<FrameworkElement, Canvas>();
        private IItemCollection<Game> subscribedGames;
        private IItemCollection<FilterPreset> subscribedFilterPresets;
        private string lastLoggedSummary;
        private bool layoutEditMode;
        private bool howLongToBeatSectionAvailable;
        private bool exophaseSectionAvailable;
        private Canvas activeLayoutCanvas;
        private Button activeLayoutEditButton;
        private Dictionary<FrameworkElement, Point> layoutEditStartPositions;
        private FrameworkElement draggedLayoutItem;
        private Canvas draggedLayoutCanvas;
        private Point dragPointerOrigin;
        private double dragItemLeftOrigin;
        private double dragItemTopOrigin;
        private int dragOriginColumn;
        private int dragOriginRow;
        private readonly DispatcherTimer smoothScrollTimer;
        private DateTime smoothScrollStartedAt;
        private double smoothScrollStartOffset;
        private double smoothScrollTargetOffset;

        internal StatsGlobalPageControl(IPlayniteAPI api, StatsSessionLedger sessionLedger)
        {
            this.api = api;
            this.sessionLedger = sessionLedger;
            InitializeComponent();
            smoothScrollTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(15)
            };
            smoothScrollTimer.Tick += OnSmoothScrollTimerTick;
            layoutPath = Path.Combine(
                api.Paths.ConfigurationPath,
                "Settings",
                "Osiris",
                "stats-layout.json");
            weeklyTrophyPath = Path.Combine(
                api.Paths.ConfigurationPath,
                "Settings",
                "Osiris",
                "stats-weekly-trophies.json");
            InitializeLayoutEditor();
            fixedHeader = CreateNativeTopPanel() ?? CreateFallbackHeader();
            fixedHeader.MinHeight = FixedHeaderHeight;
            fixedHeader.MaxHeight = FixedHeaderHeight;
            fixedHeader.HorizontalAlignment = HorizontalAlignment.Stretch;
            fixedHeader.VerticalAlignment = VerticalAlignment.Top;
            fixedHeader.Visibility = Visibility.Visible;
            FixedHeaderHost.Content = fixedHeader;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs args)
        {
            fixedHeader.Visibility = Visibility.Visible;
            var mainWindow = Application.Current?.MainWindow;
            if (mainWindow != null)
            {
                BindingOperations.SetBinding(
                    fixedHeader,
                    DataContextProperty,
                    new Binding("DataContext")
                    {
                        Source = mainWindow,
                        Mode = BindingMode.OneWay
                    });
            }

            SubscribeToLibrary();
            // Initial library subscription performs cleanup. Attach scrolling
            // afterwards so that cleanup cannot remove the active page handler.
            StatsScrollViewer.PreviewMouseWheel -= OnStatsScrollViewerPreviewMouseWheel;
            StatsScrollViewer.PreviewMouseWheel += OnStatsScrollViewerPreviewMouseWheel;
            sessionLedger.Changed -= OnSessionHistoryChanged;
            sessionLedger.Changed += OnSessionHistoryChanged;
            RefreshLibraryStats();
        }

        private void OnUnloaded(object sender, RoutedEventArgs args)
        {
            StatsScrollViewer.PreviewMouseWheel -= OnStatsScrollViewerPreviewMouseWheel;
            smoothScrollTimer.Stop();
            if (layoutEditMode)
            {
                CancelLayoutEdit();
            }
            else
            {
                RestoreDraggedItemToOrigin();
                FinishLayoutDrag();
            }

            sessionLedger.Changed -= OnSessionHistoryChanged;
            if (subscribedGames == null)
            {
                UnsubscribeFromFilterPresets();
                return;
            }

            subscribedGames.ItemCollectionChanged -= OnGamesCollectionChanged;
            subscribedGames.ItemUpdated -= OnGameUpdated;
            subscribedGames = null;
            UnsubscribeFromFilterPresets();
        }

        private void OnStatsScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs args)
        {
            if (args.Handled || StatsScrollViewer.ScrollableHeight <= 0d)
            {
                return;
            }

            var notches = args.Delta / (double)Mouse.MouseWheelDeltaForOneLine;
            if (Math.Abs(notches) < 0.01d)
            {
                return;
            }

            var currentTarget = smoothScrollTimer.IsEnabled
                ? smoothScrollTargetOffset
                : StatsScrollViewer.VerticalOffset;
            var target = Math.Max(
                0d,
                Math.Min(
                    StatsScrollViewer.ScrollableHeight,
                    currentTarget - (notches * 150d)));
            if (Math.Abs(target - currentTarget) < 0.1d)
            {
                return;
            }

            smoothScrollStartOffset = StatsScrollViewer.VerticalOffset;
            smoothScrollTargetOffset = target;
            smoothScrollStartedAt = DateTime.UtcNow;
            smoothScrollTimer.Start();
            args.Handled = true;
        }

        private void OnSmoothScrollTimerTick(object sender, EventArgs args)
        {
            var elapsed = (DateTime.UtcNow - smoothScrollStartedAt).TotalMilliseconds;
            var progress = Math.Max(0d, Math.Min(1d, elapsed / 175d));
            var easedProgress = 1d - Math.Pow(1d - progress, 3d);
            StatsScrollViewer.ScrollToVerticalOffset(
                smoothScrollStartOffset +
                ((smoothScrollTargetOffset - smoothScrollStartOffset) * easedProgress));

            if (progress >= 1d)
            {
                StatsScrollViewer.ScrollToVerticalOffset(smoothScrollTargetOffset);
                smoothScrollTimer.Stop();
            }
        }

        private void SubscribeToLibrary()
        {
            var games = api?.Database?.Games;
            if (ReferenceEquals(games, subscribedGames))
            {
                return;
            }

            OnUnloaded(this, null);
            subscribedGames = games;
            if (subscribedGames != null)
            {
                subscribedGames.ItemCollectionChanged += OnGamesCollectionChanged;
                subscribedGames.ItemUpdated += OnGameUpdated;
            }

            subscribedFilterPresets = api?.Database?.FilterPresets;
            if (subscribedFilterPresets != null)
            {
                subscribedFilterPresets.ItemCollectionChanged += OnFilterPresetsCollectionChanged;
                subscribedFilterPresets.ItemUpdated += OnFilterPresetUpdated;
            }
        }

        private void UnsubscribeFromFilterPresets()
        {
            if (subscribedFilterPresets == null)
            {
                return;
            }

            subscribedFilterPresets.ItemCollectionChanged -= OnFilterPresetsCollectionChanged;
            subscribedFilterPresets.ItemUpdated -= OnFilterPresetUpdated;
            subscribedFilterPresets = null;
        }

        private void OnGamesCollectionChanged(object sender, ItemCollectionChangedEventArgs<Game> args)
        {
            QueueRefresh();
        }

        private void OnGameUpdated(object sender, ItemUpdatedEventArgs<Game> args)
        {
            QueueRefresh();
        }

        private void OnFilterPresetsCollectionChanged(
            object sender,
            ItemCollectionChangedEventArgs<FilterPreset> args)
        {
            QueueRefresh();
        }

        private void OnFilterPresetUpdated(object sender, ItemUpdatedEventArgs<FilterPreset> args)
        {
            QueueRefresh();
        }

        private void QueueRefresh()
        {
            Dispatcher.BeginInvoke(new Action(RefreshLibraryStats));
        }

        private void OnSessionHistoryChanged(object sender, EventArgs args)
        {
            QueueRefresh();
        }

        private void WeeklyStatsHeader_Click(object sender, RoutedEventArgs args)
        {
            ToggleSection(
                WeeklyStatsContent,
                WeeklyExpandedChevron,
                WeeklyCollapsedChevron);
        }

        private void AllTimeStatsHeader_Click(object sender, RoutedEventArgs args)
        {
            ToggleSection(
                LayoutCanvas,
                AllTimeExpandedChevron,
                AllTimeCollapsedChevron);
        }

        private void HowLongToBeatStatsHeader_Click(object sender, RoutedEventArgs args)
        {
            ToggleSection(
                HowLongToBeatStatsContent,
                HowLongToBeatExpandedChevron,
                HowLongToBeatCollapsedChevron);
        }

        private void ExophaseStatsHeader_Click(object sender, RoutedEventArgs args)
        {
            ToggleSection(
                ExophaseStatsContent,
                ExophaseExpandedChevron,
                ExophaseCollapsedChevron);
        }

        private static void ToggleSection(
            FrameworkElement content,
            FrameworkElement expandedChevron,
            FrameworkElement collapsedChevron)
        {
            var collapse = content.Visibility == Visibility.Visible;
            content.Visibility = collapse ? Visibility.Collapsed : Visibility.Visible;
            expandedChevron.Visibility = collapse ? Visibility.Collapsed : Visibility.Visible;
            collapsedChevron.Visibility = collapse ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetExtensionSectionAvailability(
            FrameworkElement header,
            Canvas content,
            FrameworkElement expandedChevron,
            FrameworkElement collapsedChevron,
            bool available,
            ref bool wasAvailable)
        {
            header.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
            if (!available)
            {
                if (layoutEditMode && ReferenceEquals(activeLayoutCanvas, content))
                {
                    SaveAndFinishLayoutEdit();
                }

                content.Visibility = Visibility.Collapsed;
                expandedChevron.Visibility = Visibility.Collapsed;
                collapsedChevron.Visibility = Visibility.Visible;
            }
            else if (!wasAvailable)
            {
                content.Visibility = Visibility.Visible;
                expandedChevron.Visibility = Visibility.Visible;
                collapsedChevron.Visibility = Visibility.Collapsed;
            }

            wasAvailable = available;
        }

        private void InitializeLayoutEditor()
        {
            layoutCanvases.Add(WeeklyStatsContent);
            layoutCanvases.Add(LayoutCanvas);
            layoutCanvases.Add(HowLongToBeatStatsContent);
            layoutCanvases.Add(ExophaseStatsContent);
            foreach (var canvas in layoutCanvases)
            {
                var canvasItems = canvas.Children
                    .OfType<FrameworkElement>()
                    .Where(item => !string.IsNullOrWhiteSpace(item.Name) &&
                                   item.Name.EndsWith("Card", StringComparison.Ordinal))
                    .ToList();
                foreach (var item in canvasItems)
                {
                    layoutItems.Add(item);
                    layoutCanvasByItem[item] = canvas;
                    item.AddHandler(
                        UIElement.PreviewMouseLeftButtonDownEvent,
                        new MouseButtonEventHandler(OnLayoutItemMouseLeftButtonDown),
                        true);
                }

                canvas.PreviewMouseMove += OnLayoutCanvasMouseMove;
                canvas.PreviewMouseLeftButtonUp += OnLayoutCanvasMouseLeftButtonUp;
            }

            LoadLayoutPositions();
            UpdateAllLayoutCanvasHeights();
        }

        private void CategoryEditButton_Click(object sender, RoutedEventArgs args)
        {
            var button = sender as Button;
            var canvas = GetCategoryCanvas(button);
            if (button == null || canvas == null)
            {
                return;
            }

            if (!layoutEditMode)
            {
                BeginLayoutEdit(canvas, button);
                return;
            }

            if (!ReferenceEquals(canvas, activeLayoutCanvas))
            {
                SaveAndFinishLayoutEdit();
                BeginLayoutEdit(canvas, button);
                return;
            }

            SaveAndFinishLayoutEdit();
        }

        private void SaveAndFinishLayoutEdit()
        {
            CommitMagneticDrop();
            FinishLayoutDrag();
            SaveLayoutPositions();
            layoutEditMode = false;
            layoutEditStartPositions = null;
            activeLayoutCanvas = null;
            activeLayoutEditButton = null;
            UpdateLayoutEditorVisualState();
        }

        private void BeginLayoutEdit(Canvas canvas, Button button)
        {
            activeLayoutCanvas = canvas;
            activeLayoutEditButton = button;
            layoutEditStartPositions = GetLayoutItems(canvas).ToDictionary(
                item => item,
                item => new Point(GetCanvasLeft(item), GetCanvasTop(item)));
            layoutEditMode = true;
            UpdateLayoutEditorVisualState();
        }

        private void CancelLayoutEdit()
        {
            RestoreDraggedItemToOrigin();
            FinishLayoutDrag();
            if (layoutEditStartPositions != null)
            {
                foreach (var position in layoutEditStartPositions)
                {
                    Canvas.SetLeft(position.Key, position.Value.X);
                    Canvas.SetTop(position.Key, position.Value.Y);
                }
            }

            layoutEditMode = false;
            layoutEditStartPositions = null;
            activeLayoutCanvas = null;
            activeLayoutEditButton = null;
            UpdateAllLayoutCanvasHeights();
            UpdateLayoutEditorVisualState();
        }

        private void UpdateLayoutEditorVisualState()
        {
            foreach (var button in GetCategoryEditButtons())
            {
                button.Content = layoutEditMode && ReferenceEquals(button, activeLayoutEditButton)
                    ? "Save"
                    : "Edit";
            }

            foreach (var item in layoutItems)
            {
                Canvas itemCanvas;
                var editable = layoutEditMode &&
                               layoutCanvasByItem.TryGetValue(item, out itemCanvas) &&
                               ReferenceEquals(itemCanvas, activeLayoutCanvas);
                item.Cursor = editable ? Cursors.SizeAll : Cursors.Arrow;
                item.ToolTip = editable ? "Drag to rearrange" : null;
            }
        }

        private Canvas GetCategoryCanvas(Button button)
        {
            if (ReferenceEquals(button, WeeklyEditButton))
            {
                return WeeklyStatsContent;
            }

            if (ReferenceEquals(button, AllTimeEditButton))
            {
                return LayoutCanvas;
            }

            if (ReferenceEquals(button, HowLongToBeatEditButton))
            {
                return HowLongToBeatStatsContent;
            }

            return ReferenceEquals(button, ExophaseEditButton)
                ? ExophaseStatsContent
                : null;
        }

        private IEnumerable<Button> GetCategoryEditButtons()
        {
            yield return WeeklyEditButton;
            yield return AllTimeEditButton;
            yield return HowLongToBeatEditButton;
            yield return ExophaseEditButton;
        }

        private void OnLayoutItemMouseLeftButtonDown(object sender, MouseButtonEventArgs args)
        {
            if (!layoutEditMode || args.ChangedButton != MouseButton.Left)
            {
                return;
            }

            var item = sender as FrameworkElement;
            if (item == null)
            {
                return;
            }

            Canvas itemCanvas;
            if (!layoutCanvasByItem.TryGetValue(item, out itemCanvas) ||
                !ReferenceEquals(itemCanvas, activeLayoutCanvas))
            {
                return;
            }

            FinishLayoutDrag();
            draggedLayoutItem = item;
            draggedLayoutCanvas = itemCanvas;
            dragPointerOrigin = args.GetPosition(itemCanvas);
            dragItemLeftOrigin = GetCanvasLeft(item);
            dragItemTopOrigin = GetCanvasTop(item);
            dragOriginColumn = GetGridColumn(item);
            dragOriginRow = GetGridRow(item);
            Panel.SetZIndex(item, 1000);
            item.Opacity = 0.88d;
            Mouse.Capture(item, CaptureMode.SubTree);
            args.Handled = true;
        }

        private void OnLayoutCanvasMouseMove(object sender, MouseEventArgs args)
        {
            if (draggedLayoutItem == null || args.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            if (draggedLayoutCanvas == null)
            {
                return;
            }

            var pointer = args.GetPosition(draggedLayoutCanvas);
            var rawLeft = dragItemLeftOrigin + pointer.X - dragPointerOrigin.X;
            var rawTop = dragItemTopOrigin + pointer.Y - dragPointerOrigin.Y;
            var columnSpan = GetColumnSpan(draggedLayoutItem);
            var column = Math.Max(
                0,
                Math.Min(
                    LayoutColumnCount - columnSpan,
                    (int)Math.Round(rawLeft / LayoutColumnPitch)));
            var row = Math.Max(0, (int)Math.Round(rawTop / LayoutRowPitch));
            Canvas.SetLeft(draggedLayoutItem, column * LayoutColumnPitch);
            Canvas.SetTop(draggedLayoutItem, row * LayoutRowPitch);
            UpdateLayoutCanvasHeight(draggedLayoutCanvas);
            args.Handled = true;
        }

        private void OnLayoutCanvasMouseLeftButtonUp(object sender, MouseButtonEventArgs args)
        {
            if (draggedLayoutItem == null)
            {
                return;
            }

            CommitMagneticDrop();
            FinishLayoutDrag();
            args.Handled = true;
        }

        private void FinishLayoutDrag()
        {
            if (draggedLayoutItem == null)
            {
                return;
            }

            var item = draggedLayoutItem;
            var canvas = draggedLayoutCanvas;
            draggedLayoutItem = null;
            draggedLayoutCanvas = null;
            Mouse.Capture(null);
            item.Opacity = 1d;
            Panel.SetZIndex(item, 0);
            if (canvas != null)
            {
                UpdateLayoutCanvasHeight(canvas);
            }
        }

        private void CommitMagneticDrop()
        {
            if (draggedLayoutItem == null)
            {
                return;
            }

            var draggedRect = GetGridRect(draggedLayoutItem);
            var canvasItems = GetLayoutItems(draggedLayoutCanvas).ToList();
            var collidedRects = canvasItems
                .Where(item => !ReferenceEquals(item, draggedLayoutItem))
                .Select(GetGridRect)
                .Where(rect => GridRectsOverlap(rect, draggedRect))
                .ToList();
            if (collidedRects.Count == 0)
            {
                return;
            }

            var columnOffset = dragOriginColumn - draggedRect.Column;
            var rowOffset = dragOriginRow - draggedRect.Row;
            var movedRects = collidedRects
                .Select(rect => new LayoutGridRect
                {
                    Item = rect.Item,
                    Column = rect.Column + columnOffset,
                    Row = rect.Row + rowOffset,
                    ColumnSpan = rect.ColumnSpan,
                    RowSpan = rect.RowSpan
                })
                .ToList();
            var stationaryRects = canvasItems
                .Where(item => !ReferenceEquals(item, draggedLayoutItem) &&
                               collidedRects.All(rect => !ReferenceEquals(rect.Item, item)))
                .Select(GetGridRect)
                .ToList();
            var validDrop = movedRects.All(IsInsideGrid) &&
                            movedRects.All(rect => !GridRectsOverlap(rect, draggedRect)) &&
                            !ContainsOverlaps(movedRects) &&
                            movedRects.All(moved => stationaryRects.All(
                                stationary => !GridRectsOverlap(moved, stationary)));
            if (!validDrop)
            {
                RestoreDraggedItemToOrigin();
                return;
            }

            foreach (var movedRect in movedRects)
            {
                SetGridPosition(movedRect.Item, movedRect.Column, movedRect.Row);
            }
        }

        private IEnumerable<FrameworkElement> GetLayoutItems(Canvas canvas)
        {
            return canvas == null
                ? Enumerable.Empty<FrameworkElement>()
                : layoutItems.Where(item =>
                    layoutCanvasByItem.ContainsKey(item) &&
                    ReferenceEquals(layoutCanvasByItem[item], canvas));
        }

        private void RestoreDraggedItemToOrigin()
        {
            if (draggedLayoutItem != null)
            {
                SetGridPosition(draggedLayoutItem, dragOriginColumn, dragOriginRow);
            }
        }

        private void LoadLayoutPositions()
        {
            if (!File.Exists(layoutPath))
            {
                return;
            }

            try
            {
                var savedPositions = Serialization.FromJsonFile<List<StatsLayoutPosition>>(layoutPath);
                if (savedPositions == null)
                {
                    return;
                }

                var positionsByKey = savedPositions
                    .Where(position => position != null && !string.IsNullOrWhiteSpace(position.Key))
                    .GroupBy(position => position.Key, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);
                foreach (var canvas in layoutCanvases)
                {
                    var canvasItems = GetLayoutItems(canvas).ToList();
                    var proposedRects = new List<LayoutGridRect>();
                    foreach (var item in canvasItems)
                    {
                        StatsLayoutPosition position;
                        if (!positionsByKey.TryGetValue(item.Name, out position) ||
                            !IsFiniteNonNegative(position.Left) ||
                            !IsFiniteNonNegative(position.Top))
                        {
                            continue;
                        }

                        var columnSpan = GetColumnSpan(item);
                        proposedRects.Add(new LayoutGridRect
                        {
                            Item = item,
                            Column = Math.Max(
                                0,
                                Math.Min(
                                    LayoutColumnCount - columnSpan,
                                    (int)Math.Round(position.Left / LayoutColumnPitch))),
                            Row = Math.Max(0, (int)Math.Round(position.Top / LayoutRowPitch)),
                            ColumnSpan = columnSpan,
                            RowSpan = GetRowSpan(item)
                        });
                    }

                    NormalizeHowLongToBeatSummaryRow(canvas, proposedRects);
                    if (proposedRects.Count == 0)
                    {
                        continue;
                    }

                    if (proposedRects.Count != canvasItems.Count ||
                        proposedRects.Any(rect => !IsInsideGrid(rect)) ||
                        ContainsOverlaps(proposedRects))
                    {
                        Logger.Warn(string.Format(
                            CultureInfo.InvariantCulture,
                            "The saved Stats layout for {0} does not fit the magnetic grid. Its default layout will be used.",
                            canvas.Name));
                        continue;
                    }

                    foreach (var rect in proposedRects)
                    {
                        SetGridPosition(rect.Item, rect.Column, rect.Row);
                    }
                }
            }
            catch (Exception exception)
            {
                Logger.Warn(exception, "The saved Stats dashboard layout could not be loaded.");
            }
        }

        private void NormalizeHowLongToBeatSummaryRow(
            Canvas canvas,
            IList<LayoutGridRect> proposedRects)
        {
            if (!ReferenceEquals(canvas, HowLongToBeatStatsContent))
            {
                return;
            }

            var summaryRects = proposedRects
                .Where(rect => rect.Row == 0 &&
                               (ReferenceEquals(rect.Item, HltbGamesWithDataCard) ||
                                ReferenceEquals(rect.Item, HltbGamesWithoutDataCard) ||
                                ReferenceEquals(rect.Item, HltbTotalMainStoryCard) ||
                                ReferenceEquals(rect.Item, HltbTotalMainExtraCard) ||
                                ReferenceEquals(rect.Item, HltbTotalCompletionistCard)))
                .OrderBy(rect => rect.Column)
                .ToList();
            var legacyColumns = new[] { 0, 2, 4, 6, 8 };
            if (summaryRects.Count != legacyColumns.Length ||
                !summaryRects.Select(rect => rect.Column).SequenceEqual(legacyColumns))
            {
                return;
            }

            var nextColumn = 0;
            foreach (var rect in summaryRects)
            {
                rect.Column = nextColumn;
                nextColumn += rect.ColumnSpan;
            }
        }

        private void SaveLayoutPositions()
        {
            try
            {
                var directory = Path.GetDirectoryName(layoutPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var positions = layoutItems.Select(item => new StatsLayoutPosition
                {
                    Key = item.Name,
                    Left = Math.Round(GetCanvasLeft(item), 1),
                    Top = Math.Round(GetCanvasTop(item), 1)
                }).ToList();
                File.WriteAllText(layoutPath, Serialization.ToJson(positions));
            }
            catch (Exception exception)
            {
                Logger.Warn(exception, "The Stats dashboard layout could not be saved.");
            }
        }

        private void UpdateAllLayoutCanvasHeights()
        {
            foreach (var canvas in layoutCanvases)
            {
                UpdateLayoutCanvasHeight(canvas);
            }
        }

        private void UpdateLayoutCanvasHeight(Canvas canvas)
        {
            if (canvas == null)
            {
                return;
            }

            var canvasItems = GetLayoutItems(canvas).ToList();
            var contentBottom = canvasItems.Count == 0
                ? 157d
                : canvasItems.Max(item => GetCanvasTop(item) + GetLayoutHeight(item));
            canvas.Height = contentBottom;
        }

        private static double GetCanvasLeft(FrameworkElement item)
        {
            var value = Canvas.GetLeft(item);
            return double.IsNaN(value) ? 0d : value;
        }

        private static double GetCanvasTop(FrameworkElement item)
        {
            var value = Canvas.GetTop(item);
            return double.IsNaN(value) ? 0d : value;
        }

        private static double GetLayoutWidth(FrameworkElement item)
        {
            return item.ActualWidth > 0d
                ? item.ActualWidth
                : !double.IsNaN(item.Width) && item.Width > 0d
                    ? item.Width
                    : 150d;
        }

        private static double GetLayoutHeight(FrameworkElement item)
        {
            return item.ActualHeight > 0d
                ? item.ActualHeight
                : !double.IsNaN(item.Height) && item.Height > 0d
                    ? item.Height
                    : 157d;
        }

        private static int GetGridColumn(FrameworkElement item)
        {
            return Math.Max(0, (int)Math.Round(GetCanvasLeft(item) / LayoutColumnPitch));
        }

        private static int GetGridRow(FrameworkElement item)
        {
            return Math.Max(0, (int)Math.Round(GetCanvasTop(item) / LayoutRowPitch));
        }

        private static int GetColumnSpan(FrameworkElement item)
        {
            return Math.Max(
                1,
                (int)Math.Round((GetLayoutWidth(item) + LayoutGap) / LayoutColumnPitch));
        }

        private static int GetRowSpan(FrameworkElement item)
        {
            return Math.Max(
                1,
                (int)Math.Round((GetLayoutHeight(item) + LayoutGap) / LayoutRowPitch));
        }

        private static LayoutGridRect GetGridRect(FrameworkElement item)
        {
            return new LayoutGridRect
            {
                Item = item,
                Column = GetGridColumn(item),
                Row = GetGridRow(item),
                ColumnSpan = GetColumnSpan(item),
                RowSpan = GetRowSpan(item)
            };
        }

        private static void SetGridPosition(FrameworkElement item, int column, int row)
        {
            Canvas.SetLeft(item, column * LayoutColumnPitch);
            Canvas.SetTop(item, row * LayoutRowPitch);
        }

        private static bool IsInsideGrid(LayoutGridRect rect)
        {
            return rect.Column >= 0 &&
                   rect.Row >= 0 &&
                   rect.Column + rect.ColumnSpan <= LayoutColumnCount;
        }

        private static bool ContainsOverlaps(IList<LayoutGridRect> rects)
        {
            for (var firstIndex = 0; firstIndex < rects.Count; firstIndex++)
            {
                for (var secondIndex = firstIndex + 1; secondIndex < rects.Count; secondIndex++)
                {
                    if (GridRectsOverlap(rects[firstIndex], rects[secondIndex]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool GridRectsOverlap(LayoutGridRect first, LayoutGridRect second)
        {
            return first.Column < second.Column + second.ColumnSpan &&
                   second.Column < first.Column + first.ColumnSpan &&
                   first.Row < second.Row + second.RowSpan &&
                   second.Row < first.Row + first.RowSpan;
        }

        private static bool IsFiniteNonNegative(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
        }

        private sealed class LayoutGridRect
        {
            public FrameworkElement Item { get; set; }

            public int Column { get; set; }

            public int Row { get; set; }

            public int ColumnSpan { get; set; }

            public int RowSpan { get; set; }
        }

        private void RefreshLibraryStats()
        {
            var games = api?.Database?.Games;
            if (!IsLoaded || games == null)
            {
                return;
            }

            var allGames = games.Where(game => game != null).ToList();
            var visibleGames = allGames.Where(game => !game.Hidden).ToList();
            var visibleGameIds = new HashSet<Guid>(visibleGames.Select(game => game.Id));
            var sessions = sessionLedger.Snapshot(visibleGameIds);
            var exophase = ExophaseStatsBridge.TryCreate();
            var howLongToBeat = HowLongToBeatStatsBridge.TryCreate();
            SetExtensionSectionAvailability(
                HowLongToBeatStatsHeader,
                HowLongToBeatStatsContent,
                HowLongToBeatExpandedChevron,
                HowLongToBeatCollapsedChevron,
                howLongToBeat != null,
                ref howLongToBeatSectionAvailable);
            SetExtensionSectionAvailability(
                ExophaseStatsHeader,
                ExophaseStatsContent,
                ExophaseExpandedChevron,
                ExophaseCollapsedChevron,
                exophase != null,
                ref exophaseSectionAvailable);
            ulong totalSeconds = 0;
            ulong nativeTotalSeconds = 0;
            ulong totalPlayCount = 0;
            ulong installedLibrarySize = 0;
            long trophiesEarned = 0;
            var playedGames = 0;
            var effectivePlaytimeByGame = new Dictionary<Guid, ulong>();
            foreach (var game in visibleGames)
            {
                var effectivePlaytime = exophase?.GetStatsPlaytime(game) ?? game.Playtime;
                effectivePlaytimeByGame[game.Id] = effectivePlaytime;
                totalSeconds = SaturatingAdd(totalSeconds, effectivePlaytime);
                nativeTotalSeconds = SaturatingAdd(nativeTotalSeconds, game.Playtime);

                totalPlayCount = SaturatingAdd(totalPlayCount, game.PlayCount);
                if (game.Playtime > 0 || game.PlayCount > 0)
                {
                    playedGames++;
                }

                if (game.IsInstalled && game.InstallSize.HasValue)
                {
                    installedLibrarySize = SaturatingAdd(
                        installedLibrarySize,
                        game.InstallSize.Value);
                }

                trophiesEarned = SaturatingAdd(
                    trophiesEarned,
                    exophase?.GetEarnedTrophies(game) ?? 0);
            }

            OwnedGamesValue.Text = visibleGames.Count.ToString("N0", CultureInfo.CurrentCulture);
            PlayedGamesValue.Text = playedGames.ToString("N0", CultureInfo.CurrentCulture);
            GamesNotPlayedValue.Text = Math.Max(0, visibleGames.Count - playedGames)
                .ToString("N0", CultureInfo.CurrentCulture);
            TimePlayedValue.Text = FormatPlaytime(totalSeconds);
            TotalPlayCountValue.Text = totalPlayCount.ToString("N0", CultureInfo.CurrentCulture);
            TrophiesEarnedValue.Text = trophiesEarned.ToString("N0", CultureInfo.CurrentCulture);
            InstalledLibrarySizeValue.Text = FormatByteSize(installedLibrarySize);
            var collectionCount = api?.Database?.FilterPresets?.Count() ?? 0;
            GameCollectionsValue.Text = collectionCount.ToString("N0", CultureInfo.CurrentCulture);
            TotalFavouriteGamesValue.Text = allGames.Count(game => game.Favorite)
                .ToString("N0", CultureInfo.CurrentCulture);
            TotalHiddenGamesValue.Text = allGames.Count(game => game.Hidden)
                .ToString("N0", CultureInfo.CurrentCulture);
            InstalledGamesValue.Text = visibleGames.Count(game => game.IsInstalled)
                .ToString("N0", CultureInfo.CurrentCulture);

            var knownActivityDays = BuildKnownActivityDays(visibleGames, sessions);
            GamingDaysValue.Text = knownActivityDays.Count.ToString("N0", CultureInfo.CurrentCulture) + "D";
            var recordedSessionSeconds = sessions.Aggregate(
                0UL,
                (sum, session) => SaturatingAdd(sum, session.Seconds));
            var averageSessionSeconds = sessions.Count > 0
                ? recordedSessionSeconds / (ulong)sessions.Count
                : totalPlayCount > 0UL
                    ? nativeTotalSeconds / totalPlayCount
                    : 0UL;
            AverageSessionValue.Text = FormatPlaytime(averageSessionSeconds);
            LongestSessionValue.Text = sessions.Count > 0
                ? FormatPlaytime(sessions.Max(session => session.Seconds))
                : "—";
            LongestGamingStreakValue.Text = FormatDayCount(ComputeLongestStreak(knownActivityDays));
            FavoriteTimeValue.Text = FormatFavoriteHour(GetFavoriteHour(visibleGames, sessions));
            LibraryAgeValue.Text = FormatProfileAge(GetProfileCreationDate(visibleGames), DateTime.Now);

            RefreshWeeklyStats(
                visibleGames,
                visibleGameIds,
                sessions,
                trophiesEarned,
                exophase != null);

            var releaseDatedGames = visibleGames
                .Where(game => game.ReleaseDate.HasValue && game.ReleaseDate.Value.Date.Year > 1)
                .ToList();
            var oldestGame = releaseDatedGames
                .OrderBy(game => game.ReleaseDate.Value.Date)
                .ThenBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
                .FirstOrDefault();
            var newestGame = releaseDatedGames
                .OrderByDescending(game => game.ReleaseDate.Value.Date)
                .ThenBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
                .FirstOrDefault();
            var heaviestGame = visibleGames
                .Where(game => game.InstallSize.HasValue && game.InstallSize.Value > 0UL)
                .OrderByDescending(game => game.InstallSize.Value)
                .ThenBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
                .FirstOrDefault();
            SetGameHighlight(
                OldestGameValue,
                oldestGame,
                oldestGame == null ? (DateTime?)null : oldestGame.ReleaseDate.Value.Date);
            SetGameHighlight(
                NewestGameValue,
                newestGame,
                newestGame == null ? (DateTime?)null : newestGame.ReleaseDate.Value.Date);
            SetGameHighlight(
                HeaviestGameValue,
                heaviestGame,
                null,
                heaviestGame?.InstallSize == null
                    ? null
                    : FormatByteSize(heaviestGame.InstallSize.Value));

            TopPlayedGamesList.ItemsSource = visibleGames
                .Where(game => effectivePlaytimeByGame[game.Id] > 0UL)
                .OrderByDescending(game => effectivePlaytimeByGame[game.Id])
                .ThenBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
                .Take(5)
                .Select((game, index) => CreateGameListRow(
                    game,
                    index + 1,
                    FormatPlaytime(effectivePlaytimeByGame[game.Id])))
                .ToList();
            LastPlayedGamesList.ItemsSource = visibleGames
                .Where(game => game.LastActivity.HasValue)
                .OrderByDescending(game => game.LastActivity.Value)
                .ThenBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
                .Take(5)
                .Select((game, index) => CreateGameListRow(
                    game,
                    index + 1,
                    FormatActivityDate(game.LastActivity.Value)))
                .ToList();
            LibrariesList.ItemsSource = visibleGames
                .GroupBy(GetLibraryName, StringComparer.CurrentCultureIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase)
                .Take(5)
                .Select((group, index) => CreateLibraryListRow(
                    group.Key,
                    group.Count(),
                    index + 1))
                .ToList();
            StatusList.ItemsSource = visibleGames
                .GroupBy(
                    game => string.IsNullOrWhiteSpace(game.CompletionStatus?.Name)
                        ? "Not Played"
                        : game.CompletionStatus.Name,
                    StringComparer.CurrentCultureIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase)
                .Take(5)
                .Select((group, index) => new StatusListRow
                {
                    Rank = index + 1,
                    Name = group.Key,
                    DetailText = group.Count().ToString("N0", CultureInfo.CurrentCulture) +
                        (group.Count() == 1 ? " game" : " games")
                })
                .ToList();

            var biggestCollection = FindBiggestCollection();
            SetNamedHighlight(
                BiggestCollectionValue,
                biggestCollection?.Name,
                biggestCollection == null
                    ? null
                    : biggestCollection.GameCount.ToString("N0", CultureInfo.CurrentCulture) +
                      (biggestCollection.GameCount == 1 ? " game" : " games"));

            var favoriteDeveloper = visibleGames
                .Where(game => game.Developers != null)
                .SelectMany(game => game.Developers
                    .Where(developer => developer != null && !string.IsNullOrWhiteSpace(developer.Name))
                    .Select(developer => new
                    {
                        Name = developer.Name.Trim(),
                        Seconds = effectivePlaytimeByGame[game.Id]
                    }))
                .GroupBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(group => new
                {
                    Name = group.Key,
                    Seconds = group.Aggregate(
                        0UL,
                        (sum, item) => SaturatingAdd(sum, item.Seconds))
                })
                .OrderByDescending(item => item.Seconds)
                .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .FirstOrDefault();
            SetNamedHighlight(
                FavoriteDeveloperValue,
                favoriteDeveloper?.Name,
                favoriteDeveloper == null ? null : FormatPlaytime(favoriteDeveloper.Seconds));

            var hltbSummary = RefreshHowLongToBeatStats(visibleGames, howLongToBeat);
            var exophaseSummary = RefreshExophaseStats(visibleGames, exophase);

            var summary = string.Format(
                CultureInfo.InvariantCulture,
                "Owned={0}; Played={1}; TotalPlaytime={2}; PlayCount={3}; Trophies={4}; InstalledBytes={5}; Collections={6}; GamingDays={7}; Sessions={8}; Exophase={9}; ExophaseWithData={10}; ExophaseWithoutData={11}; ExophasePlatforms={12}; HLTB={13}; HLTBWithData={14}; HLTBWithoutData={15}; HLTBProfile={16}",
                visibleGames.Count,
                playedGames,
                FormatPlaytime(totalSeconds),
                totalPlayCount,
                trophiesEarned,
                installedLibrarySize,
                collectionCount,
                knownActivityDays.Count,
                sessions.Count,
                exophase != null,
                exophaseSummary.GamesWithData,
                exophaseSummary.GamesWithoutData,
                exophaseSummary.PlatformCount,
                howLongToBeat != null,
                hltbSummary.GamesWithData,
                hltbSummary.GamesWithoutData,
                hltbSummary.Profile);
            if (!string.Equals(summary, lastLoggedSummary, StringComparison.Ordinal))
            {
                Logger.Info("Library statistics refreshed. " + summary);
                lastLoggedSummary = summary;
            }
        }

        private HowLongToBeatSummary RefreshHowLongToBeatStats(
            IReadOnlyCollection<Game> visibleGames,
            HowLongToBeatStatsBridge bridge)
        {
            var payload = bridge?.GetStats();
            var gamesById = new Dictionary<Guid, HowLongToBeatGameData>();
            foreach (var item in payload?.Games ?? new List<HowLongToBeatGameData>())
            {
                Guid gameId;
                if (item != null && Guid.TryParse(item.GameId, out gameId))
                {
                    gamesById[gameId] = item;
                }
            }

            var rows = visibleGames
                .Select(game =>
                {
                    HowLongToBeatGameData data;
                    gamesById.TryGetValue(game.Id, out data);
                    return new HowLongToBeatGameStatRow
                    {
                        Game = game,
                        Data = data
                    };
                })
                .ToList();
            var rowsWithData = rows
                .Where(row => row.Data?.Found == true)
                .ToList();
            var totalMainStory = rowsWithData.Aggregate(
                0UL,
                (sum, row) => SaturatingAdd(
                    sum,
                    (ulong)Math.Max(0L, row.Data.MainStorySeconds)));
            var totalMainExtra = rowsWithData.Aggregate(
                0UL,
                (sum, row) => SaturatingAdd(
                    sum,
                    (ulong)Math.Max(0L, row.Data.MainExtraSeconds)));
            var totalCompletionist = rowsWithData.Aggregate(
                0UL,
                (sum, row) => SaturatingAdd(
                    sum,
                    (ulong)Math.Max(0L, row.Data.CompletionistSeconds)));

            HltbGamesWithDataValue.Text = rowsWithData.Count
                .ToString("N0", CultureInfo.CurrentCulture);
            HltbGamesWithoutDataValue.Text = Math.Max(0, visibleGames.Count - rowsWithData.Count)
                .ToString("N0", CultureInfo.CurrentCulture);
            HltbTotalMainStoryValue.Text = FormatCompletionTotal(totalMainStory);
            HltbTotalMainExtraValue.Text = FormatCompletionTotal(totalMainExtra);
            HltbTotalCompletionistValue.Text = FormatCompletionTotal(totalCompletionist);

            HltbLongestMainStoryList.ItemsSource = CreateHowLongToBeatRows(
                rowsWithData,
                row => row.Data.MainStorySeconds);
            HltbLongestMainExtraList.ItemsSource = CreateHowLongToBeatRows(
                rowsWithData,
                row => row.Data.MainExtraSeconds);
            HltbLongestCompletionistList.ItemsSource = CreateHowLongToBeatRows(
                rowsWithData,
                row => row.Data.CompletionistSeconds);

            return new HowLongToBeatSummary
            {
                GamesWithData = rowsWithData.Count,
                GamesWithoutData = Math.Max(0, visibleGames.Count - rowsWithData.Count),
                Profile = payload == null || string.IsNullOrWhiteSpace(payload.Profile)
                    ? "Unavailable"
                    : payload.Profile
            };
        }

        private List<GameListRow> CreateHowLongToBeatRows(
            IEnumerable<HowLongToBeatGameStatRow> rows,
            Func<HowLongToBeatGameStatRow, long> secondsSelector)
        {
            return rows
                .Select(row => new
                {
                    Row = row,
                    Seconds = Math.Max(0L, secondsSelector(row))
                })
                .Where(item => item.Seconds > 0L)
                .OrderByDescending(item => item.Seconds)
                .ThenBy(
                    item => GetGameName(item.Row.Game),
                    StringComparer.CurrentCultureIgnoreCase)
                .Take(5)
                .Select((item, index) => CreateGameListRow(
                    item.Row.Game,
                    index + 1,
                    FormatPlaytime((ulong)item.Seconds)))
                .ToList();
        }

        private ExophaseStatsSummary RefreshExophaseStats(
            IReadOnlyCollection<Game> visibleGames,
            ExophaseStatsBridge bridge)
        {
            var payload = bridge?.GetStats();
            var gamesById = new Dictionary<Guid, ExophaseGameData>();
            foreach (var item in payload?.Games ?? new List<ExophaseGameData>())
            {
                Guid gameId;
                if (item != null && Guid.TryParse(item.GameId, out gameId))
                {
                    gamesById[gameId] = item;
                }
            }

            var gamesWithData = visibleGames
                .Select(game =>
                {
                    ExophaseGameData data;
                    gamesById.TryGetValue(game.Id, out data);
                    return data;
                })
                .Where(data => data?.Found == true)
                .ToList();
            var platformSeconds = new Dictionary<string, ulong>(
                StringComparer.CurrentCultureIgnoreCase);
            foreach (var platform in gamesWithData
                .SelectMany(data => data.Platforms ?? new List<ExophasePlatformData>())
                .Where(platform =>
                    platform != null &&
                    platform.PlaytimeSeconds > 0UL))
            {
                var displayName = StatsPlatformVisualCatalog.Resolve(platform.Platform).DisplayName;
                ulong current;
                platformSeconds.TryGetValue(displayName, out current);
                platformSeconds[displayName] = SaturatingAdd(
                    current,
                    platform.PlaytimeSeconds);
            }

            var rankedPlatforms = platformSeconds
                .OrderByDescending(item => item.Value)
                .ThenBy(item => item.Key, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            var totalPlatformSeconds = rankedPlatforms.Aggregate(
                0UL,
                (sum, item) => SaturatingAdd(sum, item.Value));
            ExophasePlayedPlatformsList.ItemsSource = rankedPlatforms
                .Take(5)
                .Select((item, index) => CreatePlatformListRow(
                    item.Key,
                    item.Value,
                    totalPlatformSeconds,
                    index + 1))
                .ToList();

            ExophaseGamesWithDataValue.Text = gamesWithData.Count
                .ToString("N0", CultureInfo.CurrentCulture);
            ExophaseGamesWithoutDataValue.Text = Math.Max(
                    0,
                    visibleGames.Count - gamesWithData.Count)
                .ToString("N0", CultureInfo.CurrentCulture);
            ExophaseCompletedGamesValue.Text = gamesWithData.Count(data => data.Completed)
                .ToString("N0", CultureInfo.CurrentCulture);
            var mostPlayedPlatform = rankedPlatforms.FirstOrDefault();
            SetNamedHighlight(
                ExophaseMostPlayedPlatformValue,
                mostPlayedPlatform.Key,
                mostPlayedPlatform.Value == 0UL
                    ? null
                    : FormatPlaytime(mostPlayedPlatform.Value));

            return new ExophaseStatsSummary
            {
                GamesWithData = gamesWithData.Count,
                GamesWithoutData = Math.Max(0, visibleGames.Count - gamesWithData.Count),
                PlatformCount = rankedPlatforms.Count
            };
        }

        private static LibraryListRow CreatePlatformListRow(
            string platform,
            ulong seconds,
            ulong totalSeconds,
            int rank)
        {
            var visual = StatsPlatformVisualCatalog.Resolve(platform);
            var percentage = totalSeconds == 0UL
                ? 0d
                : seconds * 100d / totalSeconds;
            return new LibraryListRow
            {
                Rank = rank,
                Name = visual.DisplayName,
                DetailText = percentage.ToString("0.#", CultureInfo.CurrentCulture) +
                    "% · " + FormatPlaytime(seconds),
                IconGeometry = visual.IconGeometry,
                UseOsirisLogo = visual.UseOsirisLogo
            };
        }

        private void RefreshWeeklyStats(
            IReadOnlyCollection<Game> visibleGames,
            ISet<Guid> visibleGameIds,
            IReadOnlyList<StatsSessionRecord> sessions,
            long totalTrophies,
            bool hasExophase)
        {
            var now = DateTime.Now;
            var monday = now.Date.AddDays(-((int)now.DayOfWeek + 6) % 7);
            var weeklySecondsByGame = ReadHomeWeeklyPlaytime(monday, visibleGameIds);
            if (weeklySecondsByGame.Count == 0)
            {
                weeklySecondsByGame = sessions
                    .Where(session =>
                    {
                        var ended = session.EndedUtc.ToLocalTime();
                        return ended.Date >= monday && ended <= now;
                    })
                    .GroupBy(session => session.GameId)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Aggregate(
                            0UL,
                            (sum, session) => SaturatingAdd(sum, session.Seconds)));
            }

            var dailySeconds = new ulong[7];
            var homeEntries = ReadHomePlaytimeEntries();
            foreach (var entry in homeEntries.Where(entry =>
                         visibleGameIds.Contains(entry.GameId) &&
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
                    var ended = session.EndedUtc.ToLocalTime();
                    var index = (ended.Date - monday).Days;
                    if (index >= 0 && index < dailySeconds.Length)
                    {
                        dailySeconds[index] = SaturatingAdd(dailySeconds[index], session.Seconds);
                    }
                }
            }

            var weeklyTotal = dailySeconds.Aggregate(0UL, SaturatingAdd);
            var peak = Math.Max(1UL, dailySeconds.Max());
            WeeklyTimePlayedValue.Text = FormatWeeklyHours(weeklyTotal);
            WeeklyPlaytimeBars.ItemsSource = Enumerable.Range(0, 7)
                .Select(index =>
                {
                    var day = monday.AddDays(index);
                    return new WeeklyPlaytimeBar
                    {
                        Day = day.ToString("ddd", CultureInfo.CurrentCulture).Substring(0, 1),
                        Duration = day.ToString("dddd", CultureInfo.CurrentCulture) + ": " +
                            FormatPlaytime(dailySeconds[index]),
                        Height = dailySeconds[index] == 0UL
                            ? 4d
                            : Math.Max(7d, dailySeconds[index] / (double)peak * 53d),
                        Fill = new SolidColorBrush(day.Date == now.Date
                            ? Color.FromRgb(0x98, 0x9B, 0xA1)
                            : dailySeconds[index] == 0UL
                                ? Color.FromRgb(0x4B, 0x4D, 0x50)
                                : Color.FromRgb(0xEC, 0xEE, 0xF1))
                    };
                })
                .ToList();

            WeeklyNewGamesValue.Text = visibleGames.Count(game =>
                    game.Added.HasValue &&
                    game.Added.Value.ToLocalTime().Date >= monday &&
                    game.Added.Value.ToLocalTime() <= now)
                .ToString("N0", CultureInfo.CurrentCulture);

            var weeklySessions = sessions
                .Where(session =>
                {
                    var ended = session.EndedUtc.ToLocalTime();
                    return ended.Date >= monday && ended <= now;
                })
                .ToList();
            var weeklyFavoriteHour = weeklySessions.Count > 0
                ? GetFavoriteHour(Enumerable.Empty<Game>(), weeklySessions)
                : GetFavoriteHour(
                    visibleGames.Where(game =>
                        game.LastActivity.HasValue &&
                        game.LastActivity.Value.ToLocalTime().Date >= monday &&
                        game.LastActivity.Value.ToLocalTime() <= now),
                    new List<StatsSessionRecord>());
            WeeklyFavoriteTimeValue.Text = FormatFavoriteHour(weeklyFavoriteHour);
            WeeklyAverageSessionValue.Text = weeklySessions.Count == 0
                ? "—"
                : FormatPlaytime(
                    weeklySessions.Aggregate(
                        0UL,
                        (sum, session) => SaturatingAdd(sum, session.Seconds)) /
                    (ulong)weeklySessions.Count);

            WeeklyTrophiesEarnedValue.Text = hasExophase
                ? GetWeeklyTrophyDelta(monday, totalTrophies)
                    .ToString("N0", CultureInfo.CurrentCulture)
                : "0";

            var gameById = visibleGames.ToDictionary(game => game.Id);
            var weeklyMostPlayed = weeklySecondsByGame
                .Where(pair => pair.Value > 0UL && gameById.ContainsKey(pair.Key))
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => GetGameName(gameById[pair.Key]), StringComparer.CurrentCultureIgnoreCase)
                .FirstOrDefault();
            if (weeklyMostPlayed.Value > 0UL)
            {
                SetGameHighlight(
                    WeeklyMostPlayedGameValue,
                    gameById[weeklyMostPlayed.Key],
                    null,
                    FormatPlaytime(weeklyMostPlayed.Value));
            }
            else
            {
                SetGameHighlight(WeeklyMostPlayedGameValue, null);
            }
        }

        private Dictionary<Guid, ulong> ReadHomeWeeklyPlaytime(
            DateTime monday,
            ISet<Guid> visibleGameIds)
        {
            return ReadHomePlaytimeEntries()
                .Where(entry =>
                    visibleGameIds.Contains(entry.GameId) &&
                    entry.Day >= monday &&
                    entry.Day < monday.AddDays(7))
                .GroupBy(entry => entry.GameId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Aggregate(
                        0UL,
                        (sum, entry) => SaturatingAdd(sum, entry.Seconds)));
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

        private int GetWeeklyTrophyDelta(DateTime monday, long currentTotal)
        {
            try
            {
                WeeklyTrophySnapshot snapshot = null;
                if (File.Exists(weeklyTrophyPath))
                {
                    snapshot = Serialization.FromJsonFile<WeeklyTrophySnapshot>(weeklyTrophyPath);
                }

                if (snapshot == null || snapshot.WeekStart.Date != monday.Date)
                {
                    snapshot = new WeeklyTrophySnapshot
                    {
                        WeekStart = monday.Date,
                        Baseline = Math.Max(0L, currentTotal)
                    };
                }

                Directory.CreateDirectory(Path.GetDirectoryName(weeklyTrophyPath));
                File.WriteAllText(weeklyTrophyPath, Serialization.ToJson(snapshot));
                var earned = Math.Max(0L, currentTotal - snapshot.Baseline);
                return earned > int.MaxValue ? int.MaxValue : (int)earned;
            }
            catch (Exception exception)
            {
                Logger.Warn(exception, "The Stats weekly trophy baseline could not be read or saved.");
                return 0;
            }
        }

        private CollectionHighlight FindBiggestCollection()
        {
            try
            {
                var getFilteredGames = api.Database.GetType().GetMethod(
                    "GetFilteredGames",
                    new[] { typeof(FilterPresetSettings) });
                if (getFilteredGames == null)
                {
                    return null;
                }

                return (api.Database.FilterPresets ?? Enumerable.Empty<FilterPreset>())
                    .Where(preset => preset?.Settings != null)
                    .Select(preset => new CollectionHighlight
                    {
                        Name = string.IsNullOrWhiteSpace(preset.Name) ? "Unnamed Collection" : preset.Name,
                        GameCount = (getFilteredGames.Invoke(
                            api.Database,
                            new object[] { preset.Settings }) as IEnumerable<Game>)?.Count() ?? 0
                    })
                    .OrderByDescending(collection => collection.GameCount)
                    .ThenBy(collection => collection.Name, StringComparer.CurrentCultureIgnoreCase)
                    .FirstOrDefault();
            }
            catch (Exception exception)
            {
                Logger.Warn(exception, "The largest saved game collection could not be calculated.");
                return null;
            }
        }

        private static ulong SaturatingAdd(ulong left, ulong right)
        {
            return ulong.MaxValue - left < right ? ulong.MaxValue : left + right;
        }

        private static long SaturatingAdd(long left, int right)
        {
            var nonNegativeRight = Math.Max(0, right);
            return long.MaxValue - left < nonNegativeRight
                ? long.MaxValue
                : left + nonNegativeRight;
        }

        private static string FormatPlaytime(ulong seconds)
        {
            var totalMinutes = seconds / 60UL;
            var hours = totalMinutes / 60UL;
            var minutes = totalMinutes % 60UL;
            if (hours == 0)
            {
                return minutes.ToString(CultureInfo.CurrentCulture) + "m";
            }

            return minutes == 0
                ? hours.ToString(CultureInfo.CurrentCulture) + "h"
                : hours.ToString(CultureInfo.CurrentCulture) + "h " +
                   minutes.ToString(CultureInfo.CurrentCulture) + "m";
        }

        private static string FormatCompletionTotal(ulong seconds)
        {
            return seconds == 0UL ? "0h" : FormatPlaytime(seconds);
        }

        private static string FormatWeeklyHours(ulong seconds)
        {
            if (seconds == 0UL)
            {
                return "0hs";
            }

            if (seconds < 360UL)
            {
                return "<0.1hs";
            }

            return (seconds / 3600d).ToString("0.#", CultureInfo.CurrentCulture) + "hs";
        }

        private static string FormatByteSize(ulong bytes)
        {
            var units = new[] { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
            var value = (double)bytes;
            var unitIndex = 0;
            while (value >= 1024d && unitIndex < units.Length - 1)
            {
                value /= 1024d;
                unitIndex++;
            }

            var format = value >= 100d || unitIndex == 0
                ? "0"
                : value >= 10d
                    ? "0.0"
                    : "0.00";
            return value.ToString(format, CultureInfo.CurrentCulture) + " " + units[unitIndex];
        }

        private GameListRow CreateGameListRow(Game game, int rank, string detailText)
        {
            return new GameListRow
            {
                Rank = rank,
                Name = GetGameName(game),
                IconPath = ResolveDatabaseImagePath(game?.Icon),
                DetailText = detailText ?? string.Empty
            };
        }

        private static string GetGameName(Game game)
        {
            return game == null || string.IsNullOrWhiteSpace(game.Name)
                ? "Unnamed Game"
                : game.Name;
        }

        private static string GetLibraryName(Game game)
        {
            var sourceName = game?.Source?.Name;
            if (game?.IsCustomGame == true ||
                string.IsNullOrWhiteSpace(sourceName) ||
                string.Equals(sourceName, "Manual", StringComparison.OrdinalIgnoreCase))
            {
                return "Osiris";
            }

            return sourceName;
        }

        private static LibraryListRow CreateLibraryListRow(
            string libraryName,
            int gameCount,
            int rank)
        {
            var visual = StatsPlatformVisualCatalog.Resolve(libraryName);
            return new LibraryListRow
            {
                Rank = rank,
                Name = visual.DisplayName,
                DetailText = gameCount.ToString("N0", CultureInfo.CurrentCulture) +
                    (gameCount == 1 ? " game" : " games"),
                IconGeometry = visual.IconGeometry,
                UseOsirisLogo = visual.UseOsirisLogo
            };
        }

        private static string FormatActivityDate(DateTime value)
        {
            var local = value.Kind == DateTimeKind.Local ? value : value.ToLocalTime();
            var today = DateTime.Now.Date;
            if (local.Date == today)
            {
                return "Today";
            }

            if (local.Date == today.AddDays(-1))
            {
                return "Yesterday";
            }

            return local.Year == today.Year
                ? local.ToString("MMM d", CultureInfo.CurrentCulture)
                : local.ToString("MMM d, yyyy", CultureInfo.CurrentCulture);
        }

        private HashSet<DateTime> BuildKnownActivityDays(
            IEnumerable<Game> games,
            IReadOnlyList<StatsSessionRecord> sessions)
        {
            var days = new HashSet<DateTime>();
            foreach (var game in games.Where(game => game.LastActivity.HasValue))
            {
                days.Add(game.LastActivity.Value.ToLocalTime().Date);
            }

            foreach (var session in sessions)
            {
                var day = session.StartedUtc.ToLocalTime().Date;
                var endDay = session.EndedUtc.ToLocalTime().Date;
                while (day <= endDay)
                {
                    days.Add(day);
                    day = day.AddDays(1);
                }
            }

            foreach (var day in ReadHomePlaytimeDays())
            {
                days.Add(day);
            }

            return days;
        }

        private IEnumerable<DateTime> ReadHomePlaytimeDays()
        {
            var days = new List<DateTime>();
            try
            {
                var path = Path.Combine(
                    api.Paths.ConfigurationPath,
                    "Settings",
                    "Osiris",
                    "osiris-home-playtime.json");
                if (!File.Exists(path))
                {
                    return days;
                }

                var json = File.ReadAllText(path);
                var matches = Regex.Matches(
                    json,
                    "\\\"day\\\"\\s*:\\s*\\\"(?<day>\\d{4}-\\d{2}-\\d{2})\\\"");
                foreach (Match match in matches)
                {
                    DateTime day;
                    if (DateTime.TryParseExact(
                        match.Groups["day"].Value,
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out day))
                    {
                        days.Add(day.Date);
                    }
                }
            }
            catch (Exception exception)
            {
                Logger.Warn(exception, "Osiris Home playtime dates could not be read by Stats.");
            }

            return days;
        }

        private static int ComputeLongestStreak(IEnumerable<DateTime> knownDays)
        {
            var ordered = knownDays.Select(day => day.Date).Distinct().OrderBy(day => day).ToList();
            var longest = 0;
            var current = 0;
            DateTime? previous = null;
            foreach (var day in ordered)
            {
                current = previous.HasValue && day == previous.Value.AddDays(1)
                    ? current + 1
                    : 1;
                longest = Math.Max(longest, current);
                previous = day;
            }

            return longest;
        }

        private static string FormatDayCount(int days)
        {
            return days.ToString("N0", CultureInfo.CurrentCulture) + "D";
        }

        private static int? GetFavoriteHour(
            IEnumerable<Game> games,
            IReadOnlyList<StatsSessionRecord> sessions)
        {
            IEnumerable<int> hours = sessions.Count > 0
                ? sessions.Select(session => session.StartedUtc.ToLocalTime().Hour)
                : games
                    .Where(game => game.LastActivity.HasValue)
                    .Select(game => game.LastActivity.Value.ToLocalTime().Hour);
            var favorite = hours
                .GroupBy(hour => hour)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .FirstOrDefault();
            return favorite?.Key;
        }

        private static string FormatFavoriteHour(int? hour)
        {
            if (!hour.HasValue)
            {
                return "—";
            }

            if (hour.Value == 0)
            {
                return "Midnight";
            }

            if (hour.Value == 12)
            {
                return "Noon";
            }

            return (hour.Value % 12).ToString(CultureInfo.CurrentCulture) +
                (hour.Value < 12 ? " a.m." : " p.m.");
        }

        private DateTime GetProfileCreationDate(IEnumerable<Game> games)
        {
            var now = DateTime.Now;
            try
            {
                var created = Directory.GetCreationTime(api.Paths.ConfigurationPath);
                if (created.Year > 2000 && created <= now)
                {
                    return created;
                }
            }
            catch
            {
                // Fall back to the oldest known library addition below.
            }

            return games
                .Where(game => game.Added.HasValue)
                .Select(game => game.Added.Value.ToLocalTime())
                .DefaultIfEmpty(now)
                .Min();
        }

        private static string FormatProfileAge(DateTime created, DateTime now)
        {
            var totalDays = Math.Max(0, (now.Date - created.Date).Days);
            var years = totalDays / 365;
            var remainingDays = totalDays % 365;
            var months = remainingDays / 30;
            var days = remainingDays % 30;
            if (years > 0)
            {
                return years.ToString(CultureInfo.CurrentCulture) + "Y " +
                    months.ToString(CultureInfo.CurrentCulture) + "M";
            }

            return months > 0
                ? months.ToString(CultureInfo.CurrentCulture) + "M " +
                  days.ToString(CultureInfo.CurrentCulture) + "d"
                : days.ToString(CultureInfo.CurrentCulture) + "d";
        }

        private static void SetGameHighlight(
            TextBlock target,
            Game game,
            DateTime? date = null,
            string detail = null)
        {
            target.Text = game == null ? "—" : GetGameName(game);
            if (game == null)
            {
                target.ToolTip = null;
                return;
            }

            var suffix = date.HasValue
                ? date.Value.ToString("d", CultureInfo.CurrentCulture)
                : detail;
            target.ToolTip = string.IsNullOrWhiteSpace(suffix)
                ? GetGameName(game)
                 : GetGameName(game) + " — " + suffix;
        }

        private static void SetNamedHighlight(TextBlock target, string name, string detail)
        {
            target.Text = string.IsNullOrWhiteSpace(name) ? "—" : name;
            target.ToolTip = string.IsNullOrWhiteSpace(name)
                ? null
                : string.IsNullOrWhiteSpace(detail)
                    ? name
                    : name + " — " + detail;
        }

        private string ResolveDatabaseImagePath(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                return null;
            }

            try
            {
                var fullPath = Path.IsPathRooted(databasePath)
                    ? databasePath
                    : api.Database.GetFullFilePath(databasePath);
                return File.Exists(fullPath) ? fullPath : null;
            }
            catch
            {
                return null;
            }
        }

        private void ViewMoreTopPlayedGames_Click(object sender, RoutedEventArgs args)
        {
            try
            {
                api?.MainView?.SwitchToLibraryView();
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "Could not open the library from Top Played Games.");
            }
        }

        private sealed class StatsLayoutPosition
        {
            public string Key { get; set; }

            public double Left { get; set; }

            public double Top { get; set; }
        }

        private sealed class GameListRow
        {
            public int Rank { get; set; }

            public string Name { get; set; }

            public string IconPath { get; set; }

            public string DetailText { get; set; }
        }

        private sealed class StatusListRow
        {
            public int Rank { get; set; }

            public string Name { get; set; }

            public Geometry IconGeometry => StatsStatusIconCatalog.Get(Name);

            public string DetailText { get; set; }
        }

        private sealed class HowLongToBeatStatsPayload
        {
            public string Profile { get; set; }

            public List<HowLongToBeatGameData> Games { get; set; }
        }

        private sealed class ExophaseStatsPayload
        {
            public List<ExophaseGameData> Games { get; set; }
        }

        private sealed class ExophaseGameData
        {
            public string GameId { get; set; }

            public bool Found { get; set; }

            public bool Completed { get; set; }

            public List<ExophasePlatformData> Platforms { get; set; }
        }

        private sealed class ExophasePlatformData
        {
            public string Platform { get; set; }

            public ulong PlaytimeSeconds { get; set; }
        }

        private sealed class ExophaseStatsSummary
        {
            public int GamesWithData { get; set; }

            public int GamesWithoutData { get; set; }

            public int PlatformCount { get; set; }
        }

        private sealed class HowLongToBeatGameData
        {
            public string GameId { get; set; }

            public bool Found { get; set; }

            public long MainStorySeconds { get; set; }

            public long MainExtraSeconds { get; set; }

            public long CompletionistSeconds { get; set; }
        }

        private sealed class HowLongToBeatGameStatRow
        {
            public Game Game { get; set; }

            public HowLongToBeatGameData Data { get; set; }
        }

        private sealed class HowLongToBeatSummary
        {
            public int GamesWithData { get; set; }

            public int GamesWithoutData { get; set; }

            public string Profile { get; set; }
        }

        private sealed class WeeklyPlaytimeBar
        {
            public string Day { get; set; }

            public string Duration { get; set; }

            public double Height { get; set; }

            public Brush Fill { get; set; }
        }

        private sealed class HomePlaytimePersistence
        {
            public string GameId { get; set; }

            public string Day { get; set; }

            public string Seconds { get; set; }

            public HomePlaytimeEntry ToEntry()
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
                    ? new HomePlaytimeEntry
                    {
                        GameId = gameId,
                        Day = day.Date,
                        Seconds = seconds
                    }
                    : null;
            }
        }

        private sealed class HomePlaytimeEntry
        {
            public Guid GameId { get; set; }

            public DateTime Day { get; set; }

            public ulong Seconds { get; set; }
        }

        private sealed class WeeklyTrophySnapshot
        {
            public DateTime WeekStart { get; set; }

            public long Baseline { get; set; }
        }

        private sealed class CollectionHighlight
        {
            public string Name { get; set; }

            public int GameCount { get; set; }
        }

        private sealed class LibraryListRow
        {
            public int Rank { get; set; }

            public string Name { get; set; }

            public string DetailText { get; set; }

            public Geometry IconGeometry { get; set; }

            public bool UseOsirisLogo { get; set; }
        }

        private sealed class ExophaseStatsBridge
        {
            private readonly object plugin;
            private readonly MethodInfo statsPlaytimeMethod;
            private readonly MethodInfo earnedTrophiesMethod;
            private readonly MethodInfo statsLibraryMethod;

            private ExophaseStatsBridge(
                object plugin,
                MethodInfo statsPlaytimeMethod,
                MethodInfo earnedTrophiesMethod,
                MethodInfo statsLibraryMethod)
            {
                this.plugin = plugin;
                this.statsPlaytimeMethod = statsPlaytimeMethod;
                this.earnedTrophiesMethod = earnedTrophiesMethod;
                this.statsLibraryMethod = statsLibraryMethod;
            }

            public static ExophaseStatsBridge TryCreate()
            {
                try
                {
                    var pluginType = AppDomain.CurrentDomain
                        .GetAssemblies()
                        .Select(assembly => assembly.GetType(ExophasePluginTypeName, false))
                        .FirstOrDefault(type => type != null);
                    var plugin = pluginType?
                        .GetProperty("Current", BindingFlags.Public | BindingFlags.Static)?
                        .GetValue(null, null);
                    var playtimeMethod = pluginType?.GetMethod(
                        "GetStatsPlaytimeForOsiris",
                        BindingFlags.Instance | BindingFlags.Public);
                    var trophiesMethod = pluginType?.GetMethod(
                        "GetEarnedTrophiesForOsiris",
                        BindingFlags.Instance | BindingFlags.Public);
                    var statsLibraryMethod = pluginType?.GetMethod(
                        "GetStatsLibraryForOsiris",
                        BindingFlags.Instance | BindingFlags.Public);
                    return plugin == null || playtimeMethod == null || trophiesMethod == null
                        ? null
                        : new ExophaseStatsBridge(
                            plugin,
                            playtimeMethod,
                            trophiesMethod,
                            statsLibraryMethod);
                }
                catch
                {
                    return null;
                }
            }

            public ulong GetStatsPlaytime(Game game)
            {
                try
                {
                    return Convert.ToUInt64(
                        statsPlaytimeMethod.Invoke(plugin, new object[] { game.Id.ToString() }),
                        CultureInfo.InvariantCulture);
                }
                catch
                {
                    return game.Playtime;
                }
            }

            public int GetEarnedTrophies(Game game)
            {
                try
                {
                    return Math.Max(
                        0,
                        Convert.ToInt32(
                            earnedTrophiesMethod.Invoke(
                                plugin,
                                new object[] { game.Id.ToString() }),
                            CultureInfo.InvariantCulture));
                }
                catch
                {
                    return 0;
                }
            }

            public ExophaseStatsPayload GetStats()
            {
                if (statsLibraryMethod == null)
                {
                    return null;
                }

                try
                {
                    var json = statsLibraryMethod.Invoke(plugin, null) as string;
                    return string.IsNullOrWhiteSpace(json)
                        ? null
                        : Serialization.FromJson<ExophaseStatsPayload>(json);
                }
                catch (Exception exception)
                {
                    Logger.Warn(exception, "Exophase statistics could not be read by Stats.");
                    return null;
                }
            }
        }

        private sealed class HowLongToBeatStatsBridge
        {
            private readonly object plugin;
            private readonly MethodInfo statsLibraryMethod;

            private HowLongToBeatStatsBridge(object plugin, MethodInfo statsLibraryMethod)
            {
                this.plugin = plugin;
                this.statsLibraryMethod = statsLibraryMethod;
            }

            public static HowLongToBeatStatsBridge TryCreate()
            {
                try
                {
                    var pluginType = AppDomain.CurrentDomain
                        .GetAssemblies()
                        .Select(assembly => assembly.GetType(HowLongToBeatPluginTypeName, false))
                        .FirstOrDefault(type => type != null);
                    var plugin = pluginType?
                        .GetProperty("Current", BindingFlags.Public | BindingFlags.Static)?
                        .GetValue(null, null);
                    var statsLibraryMethod = pluginType?.GetMethod(
                        "GetStatsLibraryForOsiris",
                        BindingFlags.Instance | BindingFlags.Public);
                    return plugin == null || statsLibraryMethod == null
                        ? null
                        : new HowLongToBeatStatsBridge(plugin, statsLibraryMethod);
                }
                catch
                {
                    return null;
                }
            }

            public HowLongToBeatStatsPayload GetStats()
            {
                try
                {
                    var json = statsLibraryMethod.Invoke(plugin, null) as string;
                    return string.IsNullOrWhiteSpace(json)
                        ? null
                        : Serialization.FromJson<HowLongToBeatStatsPayload>(json);
                }
                catch (Exception exception)
                {
                    Logger.Warn(exception, "HowLongToBeat statistics could not be read by Stats.");
                    return null;
                }
            }
        }

        private static FrameworkElement CreateNativeTopPanel()
        {
            try
            {
                var topPanelType = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .Select(assembly => assembly.GetType(NativeTopPanelTypeName, false))
                    .FirstOrDefault(type => type != null);

                return topPanelType == null
                    ? null
                    : Activator.CreateInstance(topPanelType) as FrameworkElement;
            }
            catch
            {
                return null;
            }
        }

        private static FrameworkElement CreateFallbackHeader()
        {
            return new Border
            {
                Height = FixedHeaderHeight,
                Background = new SolidColorBrush(Color.FromRgb(11, 11, 11)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(28, 28, 28)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
        }
    }
}
