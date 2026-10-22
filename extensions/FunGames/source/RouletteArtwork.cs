using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Globalization;

namespace Osiris.Extensions.FunGames
{
    // Static composition only: no spin, timer, selection, betting or launch behavior.
    public sealed class RouletteArtwork : FrameworkElement
    {
        private ImageSource[] heroes = new ImageSource[0];
        private readonly ImageSource osirisMark = LoadMark();
        private static ImageSource LoadMark()
        {
            var image = new BitmapImage(new Uri("pack://application:,,,/Osiris.FunGames;component/osiris-mark.png"));
            image.Freeze(); return image;
        }
        public int HeroCount => heroes.Length;

        public void SetHeroes(IEnumerable<ImageSource> sources)
        {
            heroes = (sources ?? Enumerable.Empty<ImageSource>()).Where(s => s != null).Take(8).ToArray();
            InvalidateVisual();
        }

        private static SolidColorBrush Brush(string color)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); brush.Freeze(); return brush;
        }
        private static Point Around(Point centre, double radius, double degrees)
        {
            var angle = degrees * Math.PI / 180;
            return new Point(centre.X + Math.Cos(angle)*radius, centre.Y + Math.Sin(angle)*radius);
        }
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (ActualWidth <= 0 || ActualHeight <= 0) return;
            dc.PushClip(new RectangleGeometry(new Rect(0,0,ActualWidth,ActualHeight+12),10,10));
            // One scale for both axes: poster height must never stretch the wheel/logo.
            var scale = ActualWidth / 360;
            var posterHeight = ActualHeight / scale;
            dc.PushTransform(new ScaleTransform(scale,scale));
            var backdrop = new LinearGradientBrush();
            backdrop.StartPoint = new Point(0,0); backdrop.EndPoint = new Point(1,1);
            backdrop.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#F0E4FF"),0));
            backdrop.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#C9B4EC"),0.48));
            backdrop.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#6134A4"),1));
            dc.DrawRectangle(backdrop,null,new Rect(0,0,360,posterHeight));
            var glow = new RadialGradientBrush(Color.FromArgb(190,255,255,255),Colors.Transparent) { Center = new Point(0.78,0.05), GradientOrigin = new Point(0.78,0.05), RadiusX=0.9, RadiusY=0.8 };
            dc.DrawRectangle(glow,null,new Rect(0,0,360,posterHeight));
            var title = new FormattedText("ROULETTE", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,FontWeights.Black,FontStretches.Condensed),42,Brush("#30124D"),VisualTreeHelper.GetDpi(this).PixelsPerDip);
            var titlePoint = new Point((360-title.Width)/2,27);
            dc.DrawText(title,titlePoint);
            var underline = new LinearGradientBrush(Colors.Transparent,(Color)ColorConverter.ConvertFromString("#9B51D3"),0);
            underline.GradientStops.Add(new GradientStop(Colors.Transparent,1));
            dc.DrawRoundedRectangle(underline,null,new Rect(85,84,190,2),1,1);
            var centre = new Point(180,posterHeight-113); const double radius = 264;
            dc.DrawEllipse(Brush("#493064"),null,new Point(180,centre.Y+10),radius+9,radius+9);
            var rim = new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#C798FF"),(Color)ColorConverter.ConvertFromString("#7625DA"),90);
            dc.DrawEllipse(rim,new Pen(Brush("#F0DFFF"),2),centre,radius+9,radius+9);
            var separators = new Pen(Brush("#F7F0FF"),3) { LineJoin=PenLineJoin.Round };
            for(var index=0;index<8;index++)
            {
                var start = -112.5 + index*45;
                var wedge = new StreamGeometry();
                using(var shape=wedge.Open())
                {
                    shape.BeginFigure(centre,true,true);
                    shape.LineTo(Around(centre,radius,start),true,false);
                    shape.ArcTo(Around(centre,radius,start+45),new Size(radius,radius),0,false,SweepDirection.Clockwise,true,false);
                }
                wedge.Freeze();
                Brush fill = heroes.Length == 0
                    ? (Brush)new LinearGradientBrush((Color)ColorConverter.ConvertFromString(index%2==0 ? "#9572C6" : "#50367F"),(Color)ColorConverter.ConvertFromString("#302044"),45)
                    : new ImageBrush(heroes[index%heroes.Length]) { Stretch=Stretch.UniformToFill, AlignmentX=AlignmentX.Center,AlignmentY=AlignmentY.Center };
                dc.DrawGeometry(fill,separators,wedge);
            }
            for(var angle=0;angle<360;angle+=15)
                dc.DrawEllipse(Brush("#EFD9FF"),null,Around(centre,radius+4,angle),2,2);
            var hub = new RadialGradientBrush((Color)ColorConverter.ConvertFromString("#E9BAFF"),(Color)ColorConverter.ConvertFromString("#8229DC")) { GradientOrigin=new Point(0.3,0.15),Center=new Point(0.4,0.4),RadiusX=0.8,RadiusY=0.8 };
            dc.DrawEllipse(Brush("#55346D"),null,new Point(180,centre.Y+6),45,45);
            dc.DrawEllipse(hub,new Pen(Brush("#E3BCFF"),1.5),centre,44,44);
            dc.DrawImage(osirisMark,new Rect(153,centre.Y-27,54,54));
            var pointer = new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#CC96FF"),(Color)ColorConverter.ConvertFromString("#6D18CD"),90);
            dc.PushTransform(new TranslateTransform(0,centre.Y-radius-54));
            dc.DrawGeometry(pointer,new Pen(Brush("#DDB6FF"),1.5),Geometry.Parse("M166 37 Q180 33 194 37 Q197 38 194 44 L182 66 Q180 70 178 66 L166 44 Q163 38 166 37 Z"));
            dc.Pop();
            dc.Pop(); dc.Pop();
        }
    }
}
