using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Osiris.Extensions.Exophase
{
    public sealed class ExophasePlatformWheel : FrameworkElement
    {
        private static readonly Brush EmptyTrackBrush =
            Freeze(new SolidColorBrush(Color.FromRgb(29, 29, 29)));
        private static readonly Brush MutedSegmentBrush =
            Freeze(new SolidColorBrush(Color.FromRgb(74, 76, 82)));
        private static readonly Brush PrimaryTextBrush =
            Freeze(new SolidColorBrush(Color.FromRgb(240, 241, 247)));
        private static readonly Brush SecondaryTextBrush =
            Freeze(new SolidColorBrush(Color.FromRgb(119, 122, 130)));
        private static readonly Typeface Typeface = new Typeface(
            new FontFamily("Rajdhani"),
            FontStyles.Normal,
            FontWeights.SemiBold,
            FontStretches.Normal);

        public static readonly DependencyProperty SegmentsProperty = DependencyProperty.Register(
            nameof(Segments),
            typeof(IEnumerable<ExophasePlatformSummaryItem>),
            typeof(ExophasePlatformWheel),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnSegmentsChanged));

        public static readonly DependencyProperty HighlightedPlatformProperty =
            DependencyProperty.Register(
                nameof(HighlightedPlatform),
                typeof(string),
                typeof(ExophasePlatformWheel),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnHighlightedPlatformChanged));

        private static readonly DependencyProperty CenterOpacityProperty =
            DependencyProperty.Register(
                nameof(CenterOpacity),
                typeof(double),
                typeof(ExophasePlatformWheel),
                new FrameworkPropertyMetadata(
                    1d,
                    FrameworkPropertyMetadataOptions.AffectsRender));

        private static readonly DependencyProperty HoverExpansionProperty =
            DependencyProperty.Register(
                nameof(HoverExpansion),
                typeof(double),
                typeof(ExophasePlatformWheel),
                new FrameworkPropertyMetadata(
                    0d,
                    FrameworkPropertyMetadataOptions.AffectsRender));

        private readonly DispatcherTimer rotationTimer;
        private int displayedSegmentIndex;
        private int mouseHoveredSegmentIndex = -1;
        private int transitionVersion;

        public event EventHandler<ExophasePlatformHoverEventArgs> HoveredPlatformChanged;

        public IEnumerable<ExophasePlatformSummaryItem> Segments
        {
            get => (IEnumerable<ExophasePlatformSummaryItem>)GetValue(SegmentsProperty);
            set => SetValue(SegmentsProperty, value);
        }

        public string HighlightedPlatform
        {
            get => (string)GetValue(HighlightedPlatformProperty);
            set => SetValue(HighlightedPlatformProperty, value);
        }

        private double CenterOpacity
        {
            get => (double)GetValue(CenterOpacityProperty);
            set => SetValue(CenterOpacityProperty, value);
        }

        private double HoverExpansion
        {
            get => (double)GetValue(HoverExpansionProperty);
            set => SetValue(HoverExpansionProperty, value);
        }

        public ExophasePlatformWheel()
        {
            Width = 190;
            Height = 190;
            SnapsToDevicePixels = true;
            IsHitTestVisible = true;
            Cursor = Cursors.Arrow;

            rotationTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            rotationTimer.Tick += OnRotationTimerTick;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            var size = Math.Min(ActualWidth, ActualHeight);
            if (size <= 28)
            {
                return;
            }

            var segments = GetPositiveSegments();
            var center = new Point(ActualWidth / 2d, ActualHeight / 2d);
            var outerRadius = (size / 2d) - 13d;
            var innerRadius = outerRadius * 0.7465d;
            var ringRadius = (outerRadius + innerRadius) / 2d;
            var ringThickness = outerRadius - innerRadius;
            drawingContext.DrawEllipse(
                null,
                new Pen(EmptyTrackBrush, ringThickness),
                center,
                ringRadius,
                ringRadius);

            if (segments.Count == 1)
            {
                var highlightedSegmentIndex = FindHighlightedSegmentIndex(segments);
                var expansion = highlightedSegmentIndex == 0 ? HoverExpansion : 0d;
                drawingContext.DrawEllipse(
                    null,
                    new Pen(segments[0].Brush ?? PrimaryTextBrush, ringThickness + expansion),
                    center,
                    ringRadius + (expansion / 2d),
                    ringRadius + (expansion / 2d));
            }
            else
            {
                var startAngle = -90d;
                var highlightedSegmentIndex = FindHighlightedSegmentIndex(segments);
                for (var index = 0; index < segments.Count; index++)
                {
                    var segment = segments[index];
                    var fullSweep = Math.Max(0d, Math.Min(360d, segment.Percentage * 360d));
                    var gap = Math.Min(1.8d, fullSweep * 0.22d);
                    var visibleSweep = Math.Max(0d, fullSweep - gap);
                    if (visibleSweep > 0.05d)
                    {
                        var isHighlighted = highlightedSegmentIndex == index;
                        var expansion = isHighlighted ? HoverExpansion : 0d;
                        var brush = highlightedSegmentIndex >= 0 && !isHighlighted
                            ? MutedSegmentBrush
                            : segment.Brush ?? PrimaryTextBrush;
                        drawingContext.DrawGeometry(
                            brush,
                            null,
                            CreateRingSegment(
                                center,
                                outerRadius + expansion,
                                Math.Max(1d, innerRadius - (expansion * 0.2d)),
                                startAngle + (gap / 2d),
                                visibleSweep));
                    }

                    startAngle += fullSweep;
                }
            }

            DrawCenteredText(drawingContext, center, innerRadius, segments);
        }

        protected override void OnMouseMove(MouseEventArgs eventArgs)
        {
            base.OnMouseMove(eventArgs);
            var segments = GetPositiveSegments();
            var nextIndex = HitTestSegment(eventArgs.GetPosition(this), segments);
            if (nextIndex == mouseHoveredSegmentIndex)
            {
                return;
            }

            mouseHoveredSegmentIndex = nextIndex;
            var platform = nextIndex >= 0 ? segments[nextIndex].Platform : null;
            HighlightedPlatform = platform;
            HoveredPlatformChanged?.Invoke(
                this,
                new ExophasePlatformHoverEventArgs(platform));
        }

        protected override void OnMouseLeave(MouseEventArgs eventArgs)
        {
            base.OnMouseLeave(eventArgs);
            if (mouseHoveredSegmentIndex < 0)
            {
                return;
            }

            mouseHoveredSegmentIndex = -1;
            HighlightedPlatform = null;
            HoveredPlatformChanged?.Invoke(
                this,
                new ExophasePlatformHoverEventArgs(null));
        }

        internal static int FindSegmentAtAngle(
            IEnumerable<ExophasePlatformSummaryItem> segments,
            double clockwiseAngleFromTop)
        {
            var positiveSegments = (segments ?? Enumerable.Empty<ExophasePlatformSummaryItem>())
                .Where(item => item != null && item.Percentage > 0d)
                .ToList();
            if (positiveSegments.Count == 0)
            {
                return -1;
            }

            var normalizedAngle = ((clockwiseAngleFromTop % 360d) + 360d) % 360d;
            var accumulatedAngle = 0d;
            for (var index = 0; index < positiveSegments.Count; index++)
            {
                accumulatedAngle += Math.Max(
                    0d,
                    Math.Min(360d, positiveSegments[index].Percentage * 360d));
                if (normalizedAngle < accumulatedAngle || index == positiveSegments.Count - 1)
                {
                    return index;
                }
            }

            return -1;
        }

        private static void OnSegmentsChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs eventArgs)
        {
            var wheel = dependencyObject as ExophasePlatformWheel;
            wheel?.ResetRotation();
        }

        private static void OnHighlightedPlatformChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs eventArgs)
        {
            var wheel = dependencyObject as ExophasePlatformWheel;
            wheel?.ApplyHighlightedPlatform();
        }

        private void OnLoaded(object sender, RoutedEventArgs eventArgs)
        {
            StartRotationTimer(GetPositiveSegments());
        }

        private void OnUnloaded(object sender, RoutedEventArgs eventArgs)
        {
            rotationTimer.Stop();
            CancelCenterTransition();
        }

        private void OnRotationTimerTick(object sender, EventArgs eventArgs)
        {
            var segments = GetPositiveSegments();
            if (FindHighlightedSegmentIndex(segments) >= 0 || segments.Count < 2)
            {
                return;
            }

            var version = ++transitionVersion;
            var fadeOut = new DoubleAnimation(
                CenterOpacity,
                0d,
                TimeSpan.FromMilliseconds(160))
            {
                FillBehavior = FillBehavior.Stop
            };
            fadeOut.Completed += (completedSender, completedArgs) =>
            {
                if (version != transitionVersion ||
                    FindHighlightedSegmentIndex(segments) >= 0)
                {
                    return;
                }

                displayedSegmentIndex = (displayedSegmentIndex + 1) % segments.Count;
                CenterOpacity = 0d;
                BeginAnimation(
                    CenterOpacityProperty,
                    new DoubleAnimation(
                        0d,
                        1d,
                        TimeSpan.FromMilliseconds(220))
                    {
                        FillBehavior = FillBehavior.Stop
                    },
                    HandoffBehavior.SnapshotAndReplace);
                CenterOpacity = 1d;
                InvalidateVisual();
            };
            BeginAnimation(
                CenterOpacityProperty,
                fadeOut,
                HandoffBehavior.SnapshotAndReplace);
        }

        private void ResetRotation()
        {
            displayedSegmentIndex = 0;
            mouseHoveredSegmentIndex = -1;
            HoverExpansion = 0d;
            CancelCenterTransition();
            if (FindHighlightedSegmentIndex(GetPositiveSegments()) < 0)
            {
                HighlightedPlatform = null;
            }
            StartRotationTimer(GetPositiveSegments());
            InvalidateVisual();
        }

        private void StartRotationTimer(IReadOnlyCollection<ExophasePlatformSummaryItem> segments)
        {
            rotationTimer.Stop();
            if (IsLoaded &&
                segments != null &&
                segments.Count > 1 &&
                FindHighlightedSegmentIndex(segments) < 0)
            {
                rotationTimer.Start();
            }
        }

        private void ApplyHighlightedPlatform()
        {
            var segments = GetPositiveSegments();
            var highlightedIndex = FindHighlightedSegmentIndex(segments);
            CancelCenterTransition();
            if (highlightedIndex >= 0)
            {
                rotationTimer.Stop();
                AnimateHoverExpansion(9d);
            }
            else
            {
                AnimateHoverExpansion(0d);
                StartRotationTimer(segments);
            }

            InvalidateVisual();
        }

        private void CancelCenterTransition()
        {
            transitionVersion++;
            BeginAnimation(CenterOpacityProperty, null);
            CenterOpacity = 1d;
        }

        private void AnimateHoverExpansion(double target)
        {
            var current = HoverExpansion;
            HoverExpansion = target;
            BeginAnimation(
                HoverExpansionProperty,
                new DoubleAnimation(
                    current,
                    target,
                    TimeSpan.FromMilliseconds(150))
                {
                    EasingFunction = new QuadraticEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    FillBehavior = FillBehavior.Stop
                },
                HandoffBehavior.SnapshotAndReplace);
        }

        private int HitTestSegment(
            Point point,
            IReadOnlyList<ExophasePlatformSummaryItem> segments)
        {
            if (segments == null || segments.Count == 0)
            {
                return -1;
            }

            var size = Math.Min(ActualWidth, ActualHeight);
            var center = new Point(ActualWidth / 2d, ActualHeight / 2d);
            var outerRadius = (size / 2d) - 13d;
            var innerRadius = outerRadius * 0.7465d;
            var deltaX = point.X - center.X;
            var deltaY = point.Y - center.Y;
            var distance = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
            if (distance < innerRadius - 3d || distance > outerRadius + 12d)
            {
                return -1;
            }

            var angle = (Math.Atan2(deltaY, deltaX) * 180d / Math.PI) + 90d;
            return FindSegmentAtAngle(segments, angle);
        }

        private void DrawCenteredText(
            DrawingContext drawingContext,
            Point center,
            double innerRadius,
            IReadOnlyList<ExophasePlatformSummaryItem> segments)
        {
            if (segments == null || segments.Count == 0)
            {
                return;
            }

            var highlightedIndex = FindHighlightedSegmentIndex(segments);
            var index = highlightedIndex >= 0
                ? highlightedIndex
                : Math.Min(displayedSegmentIndex, segments.Count - 1);
            var segment = segments[index];
            var platform = CreateFittedText(
                segment.Platform ?? string.Empty,
                22d,
                PrimaryTextBrush,
                innerRadius * 1.55d);
            var percentage = CreateText(
                segment.PercentageText ?? string.Empty,
                19d,
                SecondaryTextBrush);
            var combinedHeight = platform.Height + 3d + percentage.Height;
            var top = center.Y - (combinedHeight / 2d);

            drawingContext.PushOpacity(Math.Max(0d, Math.Min(1d, CenterOpacity)));
            drawingContext.DrawText(
                platform,
                new Point(center.X - (platform.Width / 2d), top));
            drawingContext.DrawText(
                percentage,
                new Point(center.X - (percentage.Width / 2d), top + platform.Height + 3d));
            drawingContext.Pop();
        }

        private IReadOnlyList<ExophasePlatformSummaryItem> GetPositiveSegments()
        {
            return (Segments ?? Enumerable.Empty<ExophasePlatformSummaryItem>())
                .Where(item => item != null && item.PlaytimeSeconds > 0 && item.Percentage > 0d)
                .ToList();
        }

        private int FindHighlightedSegmentIndex(
            IReadOnlyCollection<ExophasePlatformSummaryItem> segments)
        {
            if (segments == null || string.IsNullOrWhiteSpace(HighlightedPlatform))
            {
                return -1;
            }

            var index = 0;
            foreach (var segment in segments)
            {
                if (string.Equals(
                    segment?.Platform,
                    HighlightedPlatform,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        private FormattedText CreateFittedText(
            string value,
            double fontSize,
            Brush brush,
            double maximumWidth)
        {
            var text = CreateText(value, fontSize, brush);
            if (text.Width > maximumWidth && maximumWidth > 0d)
            {
                text = CreateText(value, fontSize * maximumWidth / text.Width, brush);
            }

            return text;
        }

        private static Geometry CreateRingSegment(
            Point center,
            double outerRadius,
            double innerRadius,
            double startAngle,
            double sweepAngle)
        {
            var endAngle = startAngle + sweepAngle;
            var outerStart = PointOnCircle(center, outerRadius, startAngle);
            var outerEnd = PointOnCircle(center, outerRadius, endAngle);
            var innerEnd = PointOnCircle(center, innerRadius, endAngle);
            var innerStart = PointOnCircle(center, innerRadius, startAngle);
            var isLargeArc = sweepAngle > 180d;
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(outerStart, true, true);
                context.ArcTo(
                    outerEnd,
                    new Size(outerRadius, outerRadius),
                    0,
                    isLargeArc,
                    SweepDirection.Clockwise,
                    true,
                    false);
                context.LineTo(innerEnd, true, false);
                context.ArcTo(
                    innerStart,
                    new Size(innerRadius, innerRadius),
                    0,
                    isLargeArc,
                    SweepDirection.Counterclockwise,
                    true,
                    false);
            }

            geometry.Freeze();
            return geometry;
        }

        private static Point PointOnCircle(Point center, double radius, double angle)
        {
            var radians = angle * Math.PI / 180d;
            return new Point(
                center.X + (radius * Math.Cos(radians)),
                center.Y + (radius * Math.Sin(radians)));
        }

        private FormattedText CreateText(string value, double fontSize, Brush brush)
        {
            return new FormattedText(
                value ?? string.Empty,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface,
                fontSize,
                brush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
        }

        private static T Freeze<T>(T value) where T : Freezable
        {
            value.Freeze();
            return value;
        }
    }

    public sealed class ExophasePlatformHoverEventArgs : EventArgs
    {
        public string Platform { get; }

        public ExophasePlatformHoverEventArgs(string platform)
        {
            Platform = platform;
        }
    }
}
