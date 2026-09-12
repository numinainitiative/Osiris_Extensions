using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Osiris.Extensions.HowLongToBeat
{
    public sealed class HowLongToBeatTimeline : FrameworkElement
    {
        private const double TrackHeight = 12;
        private const double TrackTop = 49;
        private const double MinimumMarkerGapRatio = 0.13;
        private const double CompletionistStripeSpacing = 16;
        private const double CompletionistStripeOverscan = 6;
        private static readonly Brush EmptyTrackBrush =
            Freeze(new SolidColorBrush(Color.FromRgb(23, 23, 23)));
        private static readonly Brush MainStoryPatternBrush = CreateVerticalPatternBrush();
        private static readonly Brush MainExtraPatternBrush = CreateDiagonalPatternBrush();
        private static readonly Brush CompletionistPatternBrush = EmptyTrackBrush;
        private static readonly Brush PlayedBrush =
            Freeze(new SolidColorBrush(Color.FromRgb(244, 245, 248)));
        private static readonly Brush OutlineBrush =
            Freeze(new SolidColorBrush(Color.FromRgb(98, 98, 98)));
        private static readonly Brush LabelBrush =
            Freeze(new SolidColorBrush(Color.FromRgb(119, 122, 130)));
        private static readonly Brush ValueBrush =
            Freeze(new SolidColorBrush(Color.FromRgb(240, 241, 247)));
        private static readonly Pen MarkerOutlinePen =
            Freeze(new Pen(new SolidColorBrush(Color.FromRgb(9, 9, 9)), 3));
        private static readonly Pen MarkerPen =
            Freeze(new Pen(new SolidColorBrush(Color.FromRgb(210, 212, 217)), 1));
        private static readonly Pen TrackOutlinePen = Freeze(new Pen(OutlineBrush, 1));
        private static readonly Pen CompletionistStripePen = CreateCompletionistStripePen();
        private static readonly Typeface LabelTypeface =
            new Typeface(
                new FontFamily("Rajdhani"),
                FontStyles.Normal,
                FontWeights.SemiBold,
                FontStretches.Normal);

        public static readonly DependencyProperty PlayedSecondsProperty = Register(
            nameof(PlayedSeconds), typeof(ulong), (ulong)0);
        public static readonly DependencyProperty MainStorySecondsProperty = Register(
            nameof(MainStorySeconds), typeof(long), 0L);
        public static readonly DependencyProperty MainExtraSecondsProperty = Register(
            nameof(MainExtraSeconds), typeof(long), 0L);
        public static readonly DependencyProperty CompletionistSecondsProperty = Register(
            nameof(CompletionistSeconds), typeof(long), 0L);
        public static readonly DependencyProperty TimeProfileProperty = Register(
            nameof(TimeProfile), typeof(string), CompletionTimeProfiles.Average);

        public ulong PlayedSeconds
        {
            get => (ulong)GetValue(PlayedSecondsProperty);
            set => SetValue(PlayedSecondsProperty, value);
        }

        public long MainStorySeconds
        {
            get => (long)GetValue(MainStorySecondsProperty);
            set => SetValue(MainStorySecondsProperty, value);
        }

        public long MainExtraSeconds
        {
            get => (long)GetValue(MainExtraSecondsProperty);
            set => SetValue(MainExtraSecondsProperty, value);
        }

        public long CompletionistSeconds
        {
            get => (long)GetValue(CompletionistSecondsProperty);
            set => SetValue(CompletionistSecondsProperty, value);
        }

        public string TimeProfile
        {
            get => (string)GetValue(TimeProfileProperty);
            set => SetValue(TimeProfileProperty, value);
        }

        public HowLongToBeatTimeline()
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

            var maximum = Math.Max(
                MainStorySeconds,
                Math.Max(MainExtraSeconds, CompletionistSeconds));
            if (maximum <= 0)
            {
                return;
            }

            var track = new Rect(1, TrackTop, Math.Max(0, ActualWidth - 2), TrackHeight);
            var radius = TrackHeight / 2;
            var markers = GetMarkers(track);
            var trackClip = new RectangleGeometry(track, radius, radius);
            drawingContext.PushClip(trackClip);
            drawingContext.DrawRectangle(EmptyTrackBrush, null, track);
            DrawPatternSegments(drawingContext, track, markers);

            var playedWidth = Math.Min(track.Width, PlayedSeconds * track.Width / maximum);
            if (playedWidth > 0)
            {
                DrawPlayedFill(drawingContext, track, playedWidth, radius);
            }

            drawingContext.Pop();

            foreach (var marker in markers)
            {
                if (marker.Seconds <= 0 || marker.Position >= track.Right - 0.5)
                {
                    continue;
                }

                var x = Math.Max(track.Left + 1, Math.Min(track.Right - 1, marker.Position));
                var start = new Point(x, track.Top - 4);
                var end = new Point(x, track.Bottom + 4);
                drawingContext.DrawLine(MarkerOutlinePen, start, end);
                drawingContext.DrawLine(MarkerPen, start, end);
            }

            drawingContext.DrawRoundedRectangle(null, TrackOutlinePen, track, radius, radius);
            DrawLegend(drawingContext, markers);
        }

        private IReadOnlyList<TimelineMarker> GetMarkers(Rect track)
        {
            var ratios = CalculateSemanticMarkerRatios(
                MainStorySeconds,
                MainExtraSeconds,
                CompletionistSeconds);
            return new[]
            {
                CreateMarker(
                    "Main Story",
                    MainStorySeconds,
                    track,
                    MainStoryPatternBrush,
                    0,
                    ratios[0]),
                CreateMarker(
                    "Main + Extras",
                    MainExtraSeconds,
                    track,
                    MainExtraPatternBrush,
                    1,
                    ratios[1]),
                CreateMarker(
                    "Completionist",
                    CompletionistSeconds,
                    track,
                    CompletionistPatternBrush,
                    2,
                    ratios[2])
            };
        }

        private static TimelineMarker CreateMarker(
            string title,
            long seconds,
            Rect track,
            Brush patternBrush,
            int semanticIndex,
            double ratio)
        {
            return new TimelineMarker
            {
                Title = title,
                Seconds = seconds,
                Position = double.IsNaN(ratio)
                    ? track.Left
                    : track.Left + (track.Width * ratio),
                PatternBrush = patternBrush,
                SemanticIndex = semanticIndex
            };
        }

        internal static double[] CalculateSemanticMarkerRatios(
            long mainStorySeconds,
            long mainExtraSeconds,
            long completionistSeconds)
        {
            var seconds = new[]
            {
                Math.Max(0, mainStorySeconds),
                Math.Max(0, mainExtraSeconds),
                Math.Max(0, completionistSeconds)
            };
            var result = new[] { double.NaN, double.NaN, double.NaN };
            var maximum = seconds.Max();
            if (maximum <= 0)
            {
                return result;
            }

            var availableIndices = Enumerable.Range(0, seconds.Length)
                .Where(index => seconds[index] > 0)
                .ToList();
            if (availableIndices.Count == 1)
            {
                result[availableIndices[0]] = seconds[availableIndices[0]] / (double)maximum;
                return result;
            }

            var arranged = availableIndices
                .Select(index => seconds[index] / (double)maximum)
                .ToArray();
            for (var index = 0; index < arranged.Length; index++)
            {
                var lowerBound = index * MinimumMarkerGapRatio;
                var upperBound = 1d - ((arranged.Length - 1 - index) * MinimumMarkerGapRatio);
                arranged[index] = Math.Max(lowerBound, Math.Min(upperBound, arranged[index]));
                if (index > 0)
                {
                    arranged[index] = Math.Max(
                        arranged[index],
                        arranged[index - 1] + MinimumMarkerGapRatio);
                }
            }

            for (var index = arranged.Length - 2; index >= 0; index--)
            {
                arranged[index] = Math.Min(
                    arranged[index],
                    arranged[index + 1] - MinimumMarkerGapRatio);
            }

            for (var index = 0; index < availableIndices.Count; index++)
            {
                result[availableIndices[index]] = Math.Max(0, Math.Min(1, arranged[index]));
            }

            return result;
        }

        private static void DrawPatternSegments(
            DrawingContext drawingContext,
            Rect track,
            IEnumerable<TimelineMarker> markers)
        {
            var availableMarkers = markers
                .Where(marker => marker.Seconds > 0)
                .OrderBy(marker => marker.SemanticIndex)
                .ToList();
            var segmentLeft = track.Left;
            foreach (var marker in availableMarkers)
            {
                var segmentRight = Math.Max(
                    segmentLeft,
                    Math.Min(track.Right, marker.Position));
                if (segmentRight > segmentLeft)
                {
                    DrawPatternSegment(
                        drawingContext,
                        marker,
                        new Rect(
                            segmentLeft,
                            track.Top,
                            segmentRight - segmentLeft,
                            track.Height));
                }

                segmentLeft = segmentRight;
            }

            if (segmentLeft < track.Right && availableMarkers.Count > 0)
            {
                DrawPatternSegment(
                    drawingContext,
                    availableMarkers[availableMarkers.Count - 1],
                    new Rect(
                        segmentLeft,
                        track.Top,
                        track.Right - segmentLeft,
                        track.Height));
            }
        }

        private static void DrawPatternSegment(
            DrawingContext drawingContext,
            TimelineMarker marker,
            Rect segment)
        {
            drawingContext.DrawRectangle(marker.PatternBrush, null, segment);
            if (marker.SemanticIndex == 2)
            {
                DrawCompletionistStripes(drawingContext, segment);
            }
        }

        private static void DrawCompletionistStripes(
            DrawingContext drawingContext,
            Rect segment)
        {
            // Let the outer track clip, rather than this segment, cut the stripe
            // at the top and bottom. Overscanning both endpoints makes the cut
            // a sharp edge which meets the outline instead of exposing a cap.
            drawingContext.PushClip(new RectangleGeometry(new Rect(
                segment.Left,
                segment.Top - CompletionistStripeOverscan,
                segment.Width,
                segment.Height + (CompletionistStripeOverscan * 2))));
            for (var x = segment.Left - segment.Height;
                 x < segment.Right;
                 x += CompletionistStripeSpacing)
            {
                drawingContext.DrawLine(
                    CompletionistStripePen,
                    new Point(
                        x - CompletionistStripeOverscan,
                        segment.Bottom + CompletionistStripeOverscan),
                    new Point(
                        x + segment.Height + CompletionistStripeOverscan,
                        segment.Top - CompletionistStripeOverscan));
            }

            drawingContext.Pop();
        }

        private void DrawLegend(
            DrawingContext drawingContext,
            IReadOnlyList<TimelineMarker> markers)
        {
            var availableMarkers = markers
                .Where(marker => marker.Seconds > 0)
                .OrderBy(marker => marker.SemanticIndex)
                .ToList();
            foreach (var marker in markers)
            {
                if (marker.Seconds <= 0)
                {
                    continue;
                }

                var title = CreateText(marker.Title, 15, LabelBrush);
                var value = CreateText(CompletionTimeFormatting.Format(marker.Seconds), 16, ValueBrush);
                var gap = marker.Seconds > 0 ? 7 : 4;
                var totalWidth = title.Width + gap + value.Width;
                var markerIndex = availableMarkers.IndexOf(marker);
                var segmentLeft = markerIndex > 0
                    ? availableMarkers[markerIndex - 1].Position
                    : 1d;
                var desiredCenter = segmentLeft + ((marker.Position - segmentLeft) / 2d);
                var left = Math.Max(
                    0,
                    Math.Min(
                        Math.Max(0, ActualWidth - totalWidth),
                        desiredCenter - (totalWidth / 2d)));
                drawingContext.DrawText(
                    title,
                    new Point(left, 16));
                drawingContext.DrawText(
                    value,
                    new Point(left + title.Width + gap, 15));
            }
        }

        private string BuildToolTip()
        {
            return "Played: " +
                   CompletionTimeFormatting.Format(
                       (long)Math.Min((ulong)long.MaxValue, PlayedSeconds)) +
                   Environment.NewLine +
                   "Main Story: " + CompletionTimeFormatting.Format(MainStorySeconds) +
                   Environment.NewLine +
                   "Main + Extras: " + CompletionTimeFormatting.Format(MainExtraSeconds) +
                   Environment.NewLine +
                   "Completionist: " + CompletionTimeFormatting.Format(CompletionistSeconds) +
                   Environment.NewLine +
                   "Profile: " + (TimeProfile ?? CompletionTimeProfiles.Average);
        }

        private static void DrawPlayedFill(
            DrawingContext drawingContext,
            Rect track,
            double playedWidth,
            double radius)
        {
            if (playedWidth >= track.Width - 0.01)
            {
                drawingContext.DrawRoundedRectangle(PlayedBrush, null, track, radius, radius);
                return;
            }

            var right = track.Left + playedWidth;
            var curveRadius = Math.Min(radius, playedWidth / 2d);
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(
                    new Point(track.Left + curveRadius, track.Top),
                    true,
                    true);
                context.LineTo(new Point(right, track.Top), true, false);
                context.LineTo(new Point(right, track.Bottom), true, false);
                context.LineTo(new Point(track.Left + curveRadius, track.Bottom), true, false);
                context.ArcTo(
                    new Point(track.Left, track.Top + radius),
                    new Size(curveRadius, radius),
                    0,
                    false,
                    SweepDirection.Clockwise,
                    true,
                    false);
                context.ArcTo(
                    new Point(track.Left + curveRadius, track.Top),
                    new Size(curveRadius, radius),
                    0,
                    false,
                    SweepDirection.Clockwise,
                    true,
                    false);
            }

            geometry.Freeze();
            drawingContext.DrawGeometry(PlayedBrush, null, geometry);
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

        private static DependencyProperty Register(string name, Type type, object defaultValue)
        {
            return DependencyProperty.Register(
                name,
                type,
                typeof(HowLongToBeatTimeline),
                new FrameworkPropertyMetadata(
                    defaultValue,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnTimelinePropertyChanged));
        }

        private static void OnTimelinePropertyChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs args)
        {
            var timeline = dependencyObject as HowLongToBeatTimeline;
            if (timeline != null)
            {
                timeline.ToolTip = timeline.BuildToolTip();
            }
        }

        private static Brush CreateVerticalPatternBrush()
        {
            var drawing = CreatePatternBackground(8);
            drawing.Children.Add(new GeometryDrawing(
                new SolidColorBrush(Color.FromRgb(74, 74, 74)),
                null,
                new RectangleGeometry(new Rect(1, 0, 2, 8))));
            return CreateTiledBrush(drawing, 8);
        }

        private static Brush CreateDiagonalPatternBrush()
        {
            var drawing = CreatePatternBackground(10);
            drawing.Children.Add(new GeometryDrawing(
                new SolidColorBrush(Color.FromRgb(74, 74, 74)),
                null,
                Geometry.Parse("M0,10 L4,10 10,0 6,0 Z")));
            return CreateTiledBrush(drawing, 10);
        }

        private static Pen CreateCompletionistStripePen()
        {
            return Freeze(new Pen(
                new SolidColorBrush(Color.FromRgb(74, 74, 74)),
                4)
            {
                StartLineCap = PenLineCap.Flat,
                EndLineCap = PenLineCap.Flat
            });
        }

        private static DrawingGroup CreatePatternBackground(double tileSize)
        {
            var drawing = new DrawingGroup();
            drawing.Children.Add(new GeometryDrawing(
                new SolidColorBrush(Color.FromRgb(23, 23, 23)),
                null,
                new RectangleGeometry(new Rect(0, 0, tileSize, tileSize))));
            return drawing;
        }

        private static Brush CreateTiledBrush(Drawing drawing, double tileSize)
        {
            var brush = new DrawingBrush(drawing)
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, tileSize, tileSize),
                ViewportUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(0, 0, tileSize, tileSize),
                ViewboxUnits = BrushMappingMode.Absolute
            };
            brush.Freeze();
            return brush;
        }

        private static T Freeze<T>(T value) where T : Freezable
        {
            value.Freeze();
            return value;
        }

        private sealed class TimelineMarker
        {
            public string Title { get; set; }
            public long Seconds { get; set; }
            public double Position { get; set; }
            public Brush PatternBrush { get; set; }
            public int SemanticIndex { get; set; }
        }

    }
}
