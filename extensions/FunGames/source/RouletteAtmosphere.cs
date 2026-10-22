using System.Windows;
using System.Windows.Media;

namespace Osiris.Extensions.FunGames
{
    // Static vector light only; no frame loop, bitmap captures or input handling.
    public sealed class RouletteAtmosphere : FrameworkElement
    {
        public RouletteAtmosphere() {IsHitTestVisible=false;}
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            var w=ActualWidth;var h=ActualHeight;if(w<=0 || h<=0)return;
            dc.PushClip(new RectangleGeometry(new Rect(RenderSize),18,18));
            var glow=new RadialGradientBrush(Color.FromArgb(43,129,65,195),Colors.Transparent) {Center=new Point(0.5,0.6),GradientOrigin=new Point(0.5,0.6),RadiusX=0.65,RadiusY=0.7};
            dc.DrawRectangle(glow,null,new Rect(RenderSize));
            var teal=new RadialGradientBrush(Color.FromArgb(24,53,180,173),Colors.Transparent) {Center=new Point(0.86,0.22),GradientOrigin=new Point(0.86,0.22),RadiusX=0.6,RadiusY=0.7};
            dc.DrawRectangle(teal,null,new Rect(RenderSize));
            // Feather every beam in both directions so no rectangular panel boundary is visible.
            var fade=new RadialGradientBrush(Colors.White,Colors.Transparent) {Center=new Point(0.5,0.55),GradientOrigin=new Point(0.5,0.55),RadiusX=0.6,RadiusY=0.65};
            dc.PushOpacityMask(fade);
            for(var i=0;i<5;i++)
            {
                var end=w*(0.02+i*0.24);
                var ray=new StreamGeometry();
                using(var shape=ray.Open())
                {
                    shape.BeginFigure(new Point(w*0.5,h*1.12),true,true);
                    shape.LineTo(new Point(end-w*0.12,-h*0.12),true,false);
                    shape.LineTo(new Point(end+w*0.12,-h*0.12),true,false);
                }
                var brush=new LinearGradientBrush {StartPoint=new Point(0,0.5),EndPoint=new Point(1,0.5)};
                brush.GradientStops.Add(new GradientStop(Colors.Transparent,0));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(i%2==0?21:15),(byte)(i%2==0?162:78),(byte)(i%2==0?121:187),222),0.5));
                brush.GradientStops.Add(new GradientStop(Colors.Transparent,1));
                dc.DrawGeometry(brush,null,ray);
            }
            dc.Pop();dc.Pop();
        }
    }
}
