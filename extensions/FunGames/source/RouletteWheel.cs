using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Osiris.Extensions.FunGames
{
    // Name retained for the existing XAML contract; presentation is now a horizontal reel.
    public sealed class RouletteWheel : FrameworkElement
    {
        public static readonly DependencyProperty PositionProperty=DependencyProperty.Register(nameof(Position),typeof(double),typeof(RouletteWheel),new FrameworkPropertyMetadata(0d,FrameworkPropertyMetadataOptions.AffectsRender));
        public double Position {get=>(double)GetValue(PositionProperty);set=>SetValue(PositionProperty,value);}
        private RouletteDefinition definition;
        public bool IsSpinning {get;private set;}
        public event Action<RouletteOption> SpinCompleted;
        public RouletteWheel() {Unloaded+=(s,e)=>CancelSpin();}
        public void SetRoulette(RouletteDefinition value) {CancelSpin();definition=value?.Clone();Position=0;InvalidateVisual();}
        public void Clear() {CancelSpin();definition=null;InvalidateVisual();}
        public bool Spin()
        {
            if(IsSpinning || definition==null || definition.Validate()!=null)return false;
            var count=definition.Options.Count;
            var index=RouletteRandom.Choose(count);var choice=definition.Options[index].Clone();
            var target=RouletteRandom.LandingPosition(index,count,Position);
            var animation=new DoubleAnimation(Position,target,TimeSpan.FromSeconds(5.2)) {EasingFunction=new QuinticEase {EasingMode=EasingMode.EaseOut}};
            IsSpinning=true;
            animation.Completed+=(s,e)=>
            {
                if(!IsSpinning)return;
                BeginAnimation(PositionProperty,null);Position=target%count;IsSpinning=false;InvalidateVisual();
                SpinCompleted?.Invoke(choice);
            };
            BeginAnimation(PositionProperty,animation);return true;
        }
        public void CancelSpin()
        {
            var value=Position;BeginAnimation(PositionProperty,null);
            var count=Math.Max(1,definition?.Options.Count??1);Position=((value%count)+count)%count;IsSpinning=false;
        }
        internal static int OptionAtSlot(int slot,int count)=>(slot%count+count)%count;
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            var count=definition?.Options.Count??0;
            if(count<2 || ActualWidth<60 || ActualHeight<120)return;
            var cardWidth=Math.Max(180,Math.Min(320,ActualWidth*0.26));
            var cardHeight=Math.Max(100,Math.Min(200,Math.Min(ActualHeight-86,cardWidth/1.6)));
            var pitch=cardWidth+10;var centreX=ActualWidth/2;var top=(ActualHeight-cardHeight)/2;
            var palette=new[]{"#AE71E0","#5ECDD0","#D8AF5F","#7098DC","#DA82BC","#8678E9","#67BEA6","#BC79CE"};
            dc.PushClip(new RectangleGeometry(new Rect(RenderSize)));
            var first=(int)Math.Floor(Position)-(int)Math.Ceiling(ActualWidth/pitch/2)-1;
            var last=(int)Math.Ceiling(Position)+(int)Math.Ceiling(ActualWidth/pitch/2)+1;
            for(var slot=first;slot<=last;slot++)
            {
                var index=OptionAtSlot(slot,count);
                var color=(Color)ColorConverter.ConvertFromString(palette[index%palette.Length]);
                var x=centreX+(slot-Position)*pitch-cardWidth/2;
                var rect=new Rect(x,top,cardWidth,cardHeight);
                var fill=new LinearGradientBrush {StartPoint=new Point(0,0),EndPoint=new Point(0.8,1)};
                fill.GradientStops.Add(new GradientStop(Color.FromRgb(24,23,31),0));
                fill.GradientStops.Add(new GradientStop(Color.FromArgb(180,(byte)(color.R*0.36),(byte)(color.G*0.36),(byte)(color.B*0.36)),1));
                var isCentre=Math.Abs(slot-Position)<0.5;
                var border=isCentre?Color.FromRgb(233,221,250):Color.FromRgb(57,52,66);
                dc.DrawRoundedRectangle(fill,new Pen(new SolidColorBrush(border),isCentre?2:1),rect,10,10);
                var glow=new RadialGradientBrush(Color.FromArgb(75,color.R,color.G,color.B),Colors.Transparent);
                dc.DrawRectangle(glow,null,new Rect(x+8,top+12,cardWidth-16,cardHeight-24));
                var title=new FormattedText(definition.Options[index].Name,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal),19,Brushes.White,VisualTreeHelper.GetDpi(this).PixelsPerDip);
                title.MaxTextWidth=cardWidth-30;title.MaxLineCount=3;title.Trimming=TextTrimming.CharacterEllipsis;title.TextAlignment=TextAlignment.Center;
                dc.DrawText(title,new Point(x+15,top+(cardHeight-title.Height)/2));
                dc.DrawRoundedRectangle(new SolidColorBrush(color),null,new Rect(x+12,top+cardHeight-9,cardWidth-24,3),1.5,1.5);
            }
            var fade=new LinearGradientBrush {StartPoint=new Point(0,0.5),EndPoint=new Point(1,0.5)};
            fade.GradientStops.Add(new GradientStop(Color.FromArgb(240,9,9,12),0));
            fade.GradientStops.Add(new GradientStop(Color.FromArgb(105,9,9,12),0.18));
            fade.GradientStops.Add(new GradientStop(Colors.Transparent,0.38));
            fade.GradientStops.Add(new GradientStop(Colors.Transparent,0.62));
            fade.GradientStops.Add(new GradientStop(Color.FromArgb(105,9,9,12),0.82));
            fade.GradientStops.Add(new GradientStop(Color.FromArgb(240,9,9,12),1));
            dc.DrawRectangle(fade,null,new Rect(0,top-2,ActualWidth,cardHeight+4));
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(100,238,218,255)),1),new Point(centreX,top+6),new Point(centreX,top+cardHeight-6));
            var pointer=new StreamGeometry();
            using(var shape=pointer.Open())
            {
                shape.BeginFigure(new Point(centreX-13,top-19),true,true);
                shape.LineTo(new Point(centreX+13,top-19),true,false);
                shape.LineTo(new Point(centreX,top+1),true,false);
            }
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(221,179,255)),new Pen(Brushes.White,1),pointer);
            var bottom=new StreamGeometry();
            using(var shape=bottom.Open())
            {
                shape.BeginFigure(new Point(centreX-9,top+cardHeight+14),true,true);
                shape.LineTo(new Point(centreX+9,top+cardHeight+14),true,false);
                shape.LineTo(new Point(centreX,top+cardHeight),true,false);
            }
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(221,179,255)),null,bottom);
            dc.Pop();
        }
    }
}
