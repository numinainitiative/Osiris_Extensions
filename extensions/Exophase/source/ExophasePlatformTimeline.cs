using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Osiris.Extensions.Exophase
{
    public sealed class ExophaseTimelineSegment
    {
        public string Platform { get; set; }

        public ulong PlaytimeSeconds { get; set; }

        public Brush Brush { get; set; }

        public Brush TagBrush { get; set; }

        public Geometry IconGeometry { get; set; }

        public bool UseOsirisLogo { get; set; }
    }

    public sealed class ExophasePlatformTimeline : FrameworkElement
    {
        private const double TrackHeight = 14;
        private const double TrackTop = 54;
        private const double SegmentGap = 6;
        private const double TrophyOutlineOffset = 4;
        private const double TagTop = 9;
        private const double TagHeight = 28;
        private const double TagHorizontalPadding = 9;
        private const double IconSize = 15;
        private const double IconGap = 6;
        private const double ValueGap = 8;
        private const double PercentageGap = 6;
        private const double GroupGap = 20;
        private static readonly Brush LabelBrush =
            Freeze(new SolidColorBrush(Color.FromRgb(119, 122, 130)));
        private static readonly Brush ValueBrush =
            Freeze(new SolidColorBrush(Color.FromRgb(240, 241, 247)));
        private static readonly Brush OsirisTagForegroundBrush =
            Freeze(new SolidColorBrush(Color.FromRgb(9, 9, 9)));
        private static readonly Brush CompletedTrophyBrush =
            Freeze(new SolidColorBrush(Color.FromRgb(255, 211, 78)));
        private static readonly Brush GenericSegmentBrush =
            Freeze(new SolidColorBrush(Color.FromRgb(49, 141, 180)));
        private static readonly Brush GenericTagBrush =
            Freeze(new SolidColorBrush(Color.FromRgb(30, 73, 91)));
        private static readonly Brush OverflowBrush =
            Freeze(new SolidColorBrush(Color.FromRgb(30, 32, 37)));
        private static readonly ImageSource OsirisLogoSource = LoadOsirisLogo();
        private static readonly Brush OsirisLogoMaskBrush = CreateOsirisLogoMaskBrush();
        private static readonly Pen TrophyProgressPen = CreateTrophyProgressPen(
            Color.FromRgb(240, 241, 247));
        private static readonly Pen CompletedTrophyProgressPen = CreateTrophyProgressPen(
            Color.FromRgb(255, 211, 78));
        private static readonly Typeface LabelTypeface =
            new Typeface(
                new FontFamily("Rajdhani"),
                FontStyles.Normal,
                FontWeights.SemiBold,
                FontStretches.Normal);

        public static readonly DependencyProperty SegmentsProperty = DependencyProperty.Register(
            nameof(Segments),
            typeof(IEnumerable<ExophaseTimelineSegment>),
            typeof(ExophasePlatformTimeline),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnSegmentsChanged));

        public static readonly DependencyProperty TrophyProgressRatioProperty =
            DependencyProperty.Register(
                nameof(TrophyProgressRatio),
                typeof(double),
                typeof(ExophasePlatformTimeline),
                new FrameworkPropertyMetadata(
                    0d,
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public IEnumerable<ExophaseTimelineSegment> Segments
        {
            get => (IEnumerable<ExophaseTimelineSegment>)GetValue(SegmentsProperty);
            set => SetValue(SegmentsProperty, value);
        }

        public double TrophyProgressRatio
        {
            get => (double)GetValue(TrophyProgressRatioProperty);
            set => SetValue(TrophyProgressRatioProperty, value);
        }

        public ExophasePlatformTimeline()
        {
            Height = 76;
            MinWidth = 430;
            SnapsToDevicePixels = true;
            IsHitTestVisible = true;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            if (ActualWidth <= 4)
            {
                return;
            }

            var segments = GetPositiveSegments();
            if (segments.Count == 0)
            {
                return;
            }

            var ratios = CalculateSegmentRatios(segments.Select(segment => segment.PlaytimeSeconds));
            var track = new Rect(
                1 + TrophyOutlineOffset,
                TrackTop,
                Math.Max(0, ActualWidth - 2 - (TrophyOutlineOffset * 2)),
                TrackHeight);
            var layouts = CreateLayouts(segments, ratios, track);
            foreach (var layout in layouts)
            {
                drawingContext.DrawRectangle(
                    layout.Segment.Brush ?? GenericSegmentBrush,
                    null,
                    layout.Bounds);
            }
            DrawTrophyProgressOutline(drawingContext, track);
            DrawPlatformLabels(drawingContext, layouts);
        }

        internal static double[] CalculateSegmentRatios(IEnumerable<ulong> playtimes)
        {
            var values = (playtimes ?? Enumerable.Empty<ulong>())
                .Where(value => value > 0)
                .ToArray();
            if (values.Length == 0)
            {
                return new double[0];
            }

            var total = values.Aggregate(0d, (sum, value) => sum + value);
            if (total <= 0)
            {
                return new double[0];
            }

            return values.Select(value => value / total).ToArray();
        }

        internal static double CalculateTrophyProgressRatio(
            int earnedTrophies,
            int totalTrophies)
        {
            if (totalTrophies <= 0 || earnedTrophies <= 0)
            {
                return 0d;
            }

            return Math.Max(0d, Math.Min(1d, earnedTrophies / (double)totalTrophies));
        }

        private void DrawTrophyProgressOutline(
            DrawingContext drawingContext,
            Rect track)
        {
            var ratio = Math.Max(0d, Math.Min(1d, TrophyProgressRatio));
            if (ratio <= 0d)
            {
                return;
            }

            var outlineLeft = track.Left - TrophyOutlineOffset;
            var outlineTop = track.Top - TrophyOutlineOffset;
            var outlineBottom = track.Bottom + TrophyOutlineOffset;
            var progressRight = ratio >= 1d
                ? track.Right + TrophyOutlineOffset
                : Math.Max(outlineLeft, track.Left + (track.Width * ratio));
            var progressPen = ratio >= 1d
                ? CompletedTrophyProgressPen
                : TrophyProgressPen;
            drawingContext.DrawLine(
                progressPen,
                new Point(outlineLeft, outlineTop),
                new Point(progressRight, outlineTop));
            drawingContext.DrawLine(
                progressPen,
                new Point(outlineLeft, outlineBottom),
                new Point(progressRight, outlineBottom));
            drawingContext.DrawLine(
                progressPen,
                new Point(outlineLeft, outlineTop),
                new Point(outlineLeft, outlineBottom));
            if (ratio >= 1d)
            {
                drawingContext.DrawLine(
                    progressPen,
                    new Point(progressRight, outlineTop),
                    new Point(progressRight, outlineBottom));
            }
        }

        private static List<SegmentLayout> CreateLayouts(
            IReadOnlyList<ExophaseTimelineSegment> segments,
            IReadOnlyList<double> ratios,
            Rect track)
        {
            var result = new List<SegmentLayout>();
            var totalGapWidth = SegmentGap * Math.Max(0, segments.Count - 1);
            var availableWidth = Math.Max(0, track.Width - totalGapWidth);
            var left = track.Left;
            for (var index = 0; index < segments.Count; index++)
            {
                var right = index == segments.Count - 1
                    ? track.Right
                    : Math.Min(track.Right, left + (availableWidth * ratios[index]));
                result.Add(new SegmentLayout
                {
                    Segment = segments[index],
                    Ratio = ratios[index],
                    Bounds = new Rect(left, track.Top, Math.Max(0, right - left), track.Height)
                });
                left = right + SegmentGap;
            }

            return result;
        }

        internal static Brush CreateTagBrush(Brush platformBrush)
        {
            var solidBrush = platformBrush as SolidColorBrush;
            if (solidBrush == null)
            {
                return GenericTagBrush;
            }

            var color = solidBrush.Color;
            var factor = color.R > 230 && color.G > 230 && color.B > 230
                ? 0.24
                : color.R > 220 || color.G > 190 || color.B > 220
                    ? 0.52
                    : 0.58;
            return Freeze(new SolidColorBrush(Color.FromRgb(
                (byte)Math.Round(color.R * factor),
                (byte)Math.Round(color.G * factor),
                (byte)Math.Round(color.B * factor))));
        }

        internal static string FormatPercentage(double ratio)
        {
            var percentage = Math.Max(0, Math.Min(100, (int)Math.Round(
                ratio * 100d,
                MidpointRounding.AwayFromZero)));
            return "(" + percentage.ToString(CultureInfo.InvariantCulture) + "%)";
        }

        private void DrawPlatformLabels(
            DrawingContext drawingContext,
            IReadOnlyList<SegmentLayout> layouts)
        {
            var labels = layouts
                .Select(layout => new LegendLayout
                {
                    SegmentLayout = layout,
                    PlatformText = CreateText(
                        CompactPlatformName(layout.Segment.Platform),
                        15,
                        GetTagForegroundBrush(layout.Segment)),
                    ValueText = CreateText(
                        ExophaseActivityControl.FormatCompactPlaytime(layout.Segment.PlaytimeSeconds),
                        15,
                        ValueBrush),
                    PercentageText = CreateText(
                        FormatPercentage(layout.Ratio),
                        14,
                        TrophyProgressRatio >= 1d
                            ? CompletedTrophyBrush
                            : LabelBrush)
                })
                .ToList();

            var left = 0d;
            var shown = 0;
            for (var index = 0; index < labels.Count; index++)
            {
                var label = labels[index];
                var tagWidth = (TagHorizontalPadding * 2) + IconSize + IconGap +
                               label.PlatformText.Width;
                var groupWidth = tagWidth + ValueGap + label.ValueText.Width +
                                 PercentageGap + label.PercentageText.Width;
                var itemLeft = index == 0 ? left : left + GroupGap;
                var remaining = labels.Count - index - 1;
                var overflowReserve = remaining > 0 ? GroupGap + 42 : 0;
                if (index > 0 && itemLeft + groupWidth + overflowReserve > ActualWidth)
                {
                    break;
                }

                DrawPlatformTag(drawingContext, label, itemLeft, tagWidth);
                drawingContext.DrawText(
                    label.ValueText,
                    new Point(itemLeft + tagWidth + ValueGap, 14));
                drawingContext.DrawText(
                    label.PercentageText,
                    new Point(
                        itemLeft + tagWidth + ValueGap + label.ValueText.Width + PercentageGap,
                        15));
                left = itemLeft + groupWidth;
                shown++;
            }

            if (shown < labels.Count)
            {
                var overflowText = CreateText(
                    "+" + (labels.Count - shown).ToString(CultureInfo.InvariantCulture),
                    14,
                    ValueBrush);
                var width = Math.Max(36, overflowText.Width + 14);
                var overflowLeft = Math.Min(left + GroupGap, Math.Max(0, ActualWidth - width));
                drawingContext.DrawRoundedRectangle(
                    OverflowBrush,
                    null,
                    new Rect(overflowLeft, TagTop, width, TagHeight),
                    6,
                    6);
                drawingContext.DrawText(
                    overflowText,
                    new Point(
                        overflowLeft + ((width - overflowText.Width) / 2d),
                        TagTop + ((TagHeight - overflowText.Height) / 2d)));
            }
        }

        private void DrawPlatformTag(
            DrawingContext drawingContext,
            LegendLayout label,
            double left,
            double width)
        {
            var segment = label.SegmentLayout.Segment;
            drawingContext.DrawRoundedRectangle(
                segment.TagBrush ?? CreateTagBrush(segment.Brush),
                null,
                new Rect(left, TagTop, width, TagHeight),
                6,
                6);
            DrawPlatformIcon(
                drawingContext,
                segment,
                left + TagHorizontalPadding,
                TagTop + ((TagHeight - IconSize) / 2d));
            drawingContext.DrawText(
                label.PlatformText,
                new Point(
                    left + TagHorizontalPadding + IconSize + IconGap,
                    TagTop + ((TagHeight - label.PlatformText.Height) / 2d)));
        }

        private static void DrawPlatformIcon(
            DrawingContext drawingContext,
            ExophaseTimelineSegment segment,
            double left,
            double top)
        {
            if (segment.UseOsirisLogo && OsirisLogoSource != null)
            {
                var logoBounds = new Rect(left, top, IconSize, IconSize);
                drawingContext.PushOpacityMask(OsirisLogoMaskBrush);
                drawingContext.DrawRectangle(
                    OsirisTagForegroundBrush,
                    null,
                    logoBounds);
                drawingContext.Pop();
                return;
            }

            var geometry = segment.IconGeometry;
            if (geometry == null || geometry.Bounds.Width <= 0 || geometry.Bounds.Height <= 0)
            {
                return;
            }

            var bounds = geometry.Bounds;
            var scale = Math.Min(IconSize / bounds.Width, IconSize / bounds.Height);
            var renderedWidth = bounds.Width * scale;
            var renderedHeight = bounds.Height * scale;
            var destinationLeft = left + ((IconSize - renderedWidth) / 2d);
            var destinationTop = top + ((IconSize - renderedHeight) / 2d);
            var transform = new MatrixTransform(new Matrix(
                scale,
                0,
                0,
                scale,
                destinationLeft - (bounds.X * scale),
                destinationTop - (bounds.Y * scale)));
            drawingContext.PushTransform(transform);
            drawingContext.DrawGeometry(GetTagForegroundBrush(segment), null, geometry);
            drawingContext.Pop();
        }

        private static Brush GetTagForegroundBrush(ExophaseTimelineSegment segment)
        {
            return segment?.UseOsirisLogo == true
                ? OsirisTagForegroundBrush
                : ValueBrush;
        }

        private static string CompactPlatformName(string platform)
        {
            switch (platform ?? string.Empty)
            {
                case "PlayStation 3":
                    return "PS3";
                case "PlayStation 4":
                    return "PS4";
                case "PlayStation 5":
                    return "PS5";
                case "Nintendo Switch":
                    return "Switch";
                case "Integrated Library":
                    return "Integrated";
                default:
                    var value = platform ?? "Unknown";
                    return value.Length > 12 ? value.Substring(0, 11) + "…" : value;
            }
        }

        private List<ExophaseTimelineSegment> GetPositiveSegments()
        {
            return (Segments ?? Enumerable.Empty<ExophaseTimelineSegment>())
                .Where(segment => segment != null && segment.PlaytimeSeconds > 0)
                .ToList();
        }

        private string BuildToolTip()
        {
            var segments = GetPositiveSegments();
            if (segments.Count == 0)
            {
                return null;
            }

            var total = segments.Aggregate(
                0UL,
                (sum, segment) => ulong.MaxValue - sum < segment.PlaytimeSeconds
                    ? ulong.MaxValue
                    : sum + segment.PlaytimeSeconds);
            var result = new StringBuilder();
            result.Append("Total: ");
            result.Append(ExophaseActivityControl.FormatCompactPlaytime(total));
            foreach (var segment in segments)
            {
                result.AppendLine();
                result.Append(segment.Platform);
                result.Append(": ");
                result.Append(ExophaseActivityControl.FormatCompactPlaytime(segment.PlaytimeSeconds));
                result.Append(" ");
                result.Append(FormatPercentage(segment.PlaytimeSeconds / (double)total));
            }

            return result.ToString();
        }

        private FormattedText CreateText(string value, double fontSize, Brush brush)
        {
            return new FormattedText(
                value ?? string.Empty,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                LabelTypeface,
                fontSize,
                brush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
        }

        private static void OnSegmentsChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs args)
        {
            var timeline = dependencyObject as ExophasePlatformTimeline;
            if (timeline != null)
            {
                timeline.ToolTip = timeline.BuildToolTip();
            }
        }

        private static T Freeze<T>(T value) where T : Freezable
        {
            value.Freeze();
            return value;
        }

        private static ImageSource LoadOsirisLogo()
        {
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri(
                    "pack://application:,,,/Osiris.Exophase;component/Assets/osiris-logo.png",
                    UriKind.Absolute);
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }

        private static Brush CreateOsirisLogoMaskBrush()
        {
            if (OsirisLogoSource == null)
            {
                return null;
            }

            return Freeze(new ImageBrush(OsirisLogoSource)
            {
                Stretch = Stretch.Uniform
            });
        }

        private static Pen CreateTrophyProgressPen(Color color)
        {
            var pen = new Pen(
                new SolidColorBrush(color),
                2)
            {
                DashStyle = new DashStyle(new[] { 3d, 2d }, 0),
                DashCap = PenLineCap.Flat,
                StartLineCap = PenLineCap.Flat,
                EndLineCap = PenLineCap.Flat,
                LineJoin = PenLineJoin.Miter
            };
            pen.Freeze();
            return pen;
        }

        private sealed class SegmentLayout
        {
            public ExophaseTimelineSegment Segment { get; set; }

            public double Ratio { get; set; }

            public Rect Bounds { get; set; }
        }

        private sealed class LegendLayout
        {
            public SegmentLayout SegmentLayout { get; set; }

            public FormattedText PlatformText { get; set; }

            public FormattedText ValueText { get; set; }

            public FormattedText PercentageText { get; set; }
        }
    }
}
