using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Osiris.Extensions.FunGames
{
    // Finite, decorative animation: no dispatcher timer or global rendering subscription.
    public sealed class WinnerConfetti : FrameworkElement
    {
        public static readonly DependencyProperty ProgressProperty=DependencyProperty.Register(nameof(Progress),typeof(double),typeof(WinnerConfetti),new FrameworkPropertyMetadata(1d,FrameworkPropertyMetadataOptions.AffectsRender));
        public double Progress {get=>(double)GetValue(ProgressProperty);set=>SetValue(ProgressProperty,value);}
        public bool IsRunning {get;private set;}
        private readonly Particle[] particles=new Particle[64];
        private sealed class Particle {public double X,Delay,Drift,Speed,Size,Rotation;public Brush Brush;}
        public WinnerConfetti()
        {
            IsHitTestVisible=false;
            var random=new Random(47921);
            var colors=new[]{"#B878FF","#5DE0CF","#F2C766","#E987CB","#77BDEB"};
            for(var i=0;i<particles.Length;i++)
            {
                var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i%colors.Length]));brush.Freeze();
                particles[i]=new Particle {X=random.NextDouble(),Delay=random.NextDouble()*0.22,Drift=(random.NextDouble()-0.5)*180,Speed=0.8+random.NextDouble()*0.5,Size=4+random.NextDouble()*5,Rotation=random.NextDouble()*360,Brush=brush};
            }
            Unloaded+=(s,e)=>Stop();
        }
        public void Start()
        {
            Stop();Progress=0;IsRunning=true;
            var animation=new DoubleAnimation(0,1,TimeSpan.FromSeconds(2.8));
            animation.Completed+=(s,e)=>Stop();
            BeginAnimation(ProgressProperty,animation);
        }
        public void Stop() {BeginAnimation(ProgressProperty,null);Progress=1;IsRunning=false;}
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if(Progress<=0 || Progress>=1 || ActualWidth<=0 || ActualHeight<=0)return;
            dc.PushClip(new RectangleGeometry(new Rect(RenderSize)));
            dc.PushOpacity(Math.Min(1,(1-Progress)*4));
            foreach(var particle in particles)
            {
                var t=Progress-particle.Delay;if(t<0)continue;
                var centre=new Point(particle.X*ActualWidth+particle.Drift*t,-20+(ActualHeight+100)*t*particle.Speed);
                dc.PushTransform(new RotateTransform(particle.Rotation+t*500,centre.X,centre.Y));
                dc.DrawRoundedRectangle(particle.Brush,null,new Rect(centre.X-particle.Size/2,centre.Y-particle.Size,particle.Size,particle.Size*1.8),1,1);
                dc.Pop();
            }
            dc.Pop();dc.Pop();
        }
    }
}
