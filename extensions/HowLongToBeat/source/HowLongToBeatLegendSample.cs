using System.Windows;
using System.Windows.Media;

namespace Osiris.Extensions.HowLongToBeat
{
    public enum HowLongToBeatLegendKind
    {
        TimePlayed,
        MainStory,
        MainExtra,
        Completionist
    }

    public sealed class HowLongToBeatLegendSample : FrameworkElement
    {
        public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
            nameof(Kind), typeof(HowLongToBeatLegendKind), typeof(HowLongToBeatLegendSample),
            new FrameworkPropertyMetadata(HowLongToBeatLegendKind.TimePlayed,
                FrameworkPropertyMetadataOptions.AffectsRender));

        public HowLongToBeatLegendKind Kind
        {
            get => (HowLongToBeatLegendKind)GetValue(KindProperty);
            set => SetValue(KindProperty, value);
        }

        public HowLongToBeatLegendSample()
        {
            SnapsToDevicePixels = true;
            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            HowLongToBeatTimeline.DrawLegendSample(
                drawingContext, new Rect(0, 0, ActualWidth, ActualHeight), Kind);
        }
    }
}
