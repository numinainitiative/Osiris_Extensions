using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using Osiris.Extensions.FunGames;
using Playnite.SDK.Plugins;

internal static class Program
{
    static int checks;
    static void Expect(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static IEnumerable<Drawing> Walk(Drawing drawing)
    {
        yield return drawing;
        var group = drawing as DrawingGroup;
        if (group != null) foreach(var child in group.Children) foreach(var node in Walk(child)) yield return node;
    }
    static DrawingGroup Draw(RouletteArtwork art)
    {
        art.Measure(new Size(358,450)); art.Arrange(new Rect(0,0,358,450));
        var visual = new DrawingVisual();
        using(var dc=visual.RenderOpen()) typeof(RouletteArtwork).GetMethod("OnRender",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(art,new object[]{dc});
        return visual.Drawing;
    }
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var item = new FunGamesSidebarItem();
            Expect(item.OsirisGlobalPage && item.Title == "Fun Games" && item.Type == SiderbarItemType.View, "Global page discovery contract");
            Expect(File.Exists(item.OsirisGlobalPageIconPath), "Joystick icon included");
            var bitmap = new BitmapImage(new Uri(item.OsirisGlobalPageIconPath));
            Expect(bitmap.PixelWidth == 128 && bitmap.PixelHeight == 128, "Icon dimensions");
            var page = (FunGamesPage)item.Opened();
            Expect(page != null && !ReferenceEquals(page,item.Opened()), "Opens independent page controls");
            var choices = (WrapPanel)page.FindName("GameChoices");
            var card = (Border)page.FindName("RouletteCard");
            var play = (Button)page.FindName("RoulettePlayButton");
            var catalog = (ScrollViewer)page.FindName("GamesCatalog");
            var art = (RouletteArtwork)page.FindName("WheelArtwork");
            Expect(choices.Children.Count == 1 && choices.Children[0] == card, "Exactly one Roulette card, expandable hub");
            Expect(card.Width == 360 && card.Height == 560 && card.Height > card.Width, "Larger portrait card");
            Expect(card.CornerRadius.TopLeft == 14 && card.BorderThickness.Left == 1 && card.Padding.Left == 10, "Reference card shell and inset");
            Expect(catalog.Visibility == Visibility.Visible && page.FindName("RoulettePlaceholder") == null, "No placeholder navigation");
            Expect(catalog.VerticalScrollBarVisibility == ScrollBarVisibility.Auto && catalog.HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled, "Full-page vertical scrolling");
            Expect((string)play.Content == "Play" && play.Command == null && play.IsEnabled, "Play opens the Roulette page through its handler");
            Expect(System.Windows.Automation.AutomationProperties.GetName(play) == "Play Roulette", "Accessible Play name");
            Expect(play.Style == page.FindResource("FunGamesPlayButtonStyle") && play.Style.BasedOn == page.FindResource("OsirisExtensionBrowsePrimaryButtonStyle"), "Play variant retains Extensions browse base");
            play.ApplyTemplate();
            var chrome = (Border)play.Template.FindName("PlayBackground",play);
            Expect(chrome != null && chrome.CornerRadius.TopLeft == 0 && chrome.CornerRadius.BottomLeft == 9 && play.Height == 52 && play.FontSize == 22, "Larger Play label with reference geometry");
            Expect(((SolidColorBrush)play.Background).Color == (Color)ColorConverter.ConvertFromString("#303030") && ((SolidColorBrush)play.Foreground).Color == (Color)ColorConverter.ConvertFromString("#F0F1F7"), "Play is grey with light text at rest");
            var hover = play.Template.Triggers.OfType<Trigger>().Single(t=>t.Property == UIElement.IsMouseOverProperty);
            Expect(hover.Setters.OfType<Setter>().Any(s=>s.Property==Border.BackgroundProperty && ((SolidColorBrush)s.Value).Color==Colors.White), "Play background turns white only on hover");
            Expect(play.Template.Triggers.Count == 3, "Canonical hover, press, disabled states");
            for(int i=0;i<3;i++)
            {
                play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var host=(ContentControl)page.FindName("RouletteHost");
                Expect(catalog.Visibility == Visibility.Collapsed && host.Content is RoulettePage,"Play opens working Roulette page");
                Expect(((RoulettePage)host.Content).FindName("BackButton")==null,"No duplicate Back to games control");
                Expect(page.OsirisTryNavigateBack(),"Search-panel Back contract consumes nested navigation");
                Expect(catalog.Visibility == Visibility.Visible && host.Content==null,"Back releases Roulette page and restores hub");
            }
            Expect(!art.IsHitTestVisible && art.ClipToBounds, "Artwork is noninteractive and clipped");
            var emptyDrawing=Draw(art);
            foreach(var size in new[]{new Size(338,476),new Size(360,450),new Size(420,600)})
            {
                art.Measure(size); art.Arrange(new Rect(size));
                var v=new DrawingVisual();
                using(var dc=v.RenderOpen()) typeof(RouletteArtwork).GetMethod("OnRender",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(art,new object[]{dc});
                var transforms=Walk(v.Drawing).OfType<DrawingGroup>().Select(g=>g.Transform).OfType<ScaleTransform>().ToArray();
                Expect(transforms.Length==1 && transforms.All(t=>Math.Abs(t.ScaleX-t.ScaleY)<0.00001), "Wheel and Osiris mark stay circular at every poster aspect ratio");
            }
            Expect(Walk(emptyDrawing).OfType<GlyphRunDrawing>().Any(),"Roulette title integrated into poster drawing");
            Expect(Walk(emptyDrawing).OfType<ImageDrawing>().Count()==1,"Actual Osiris logo embedded in wheel hub");
            Expect(Walk(emptyDrawing).OfType<GeometryDrawing>().All(d => !(d.Brush is ImageBrush)), "Missing artwork has safe decorative fallback");
            Expect(Walk(emptyDrawing).OfType<GeometryDrawing>().Count(d => d.Brush is LinearGradientBrush) >= 10,"Reference-inspired gradient wheel, rim and pointer drawn");
            var hero = BitmapSource.Create(2,2,96,96,PixelFormats.Bgr32,null,new byte[16],8); hero.Freeze();
            art.SetHeroes(Enumerable.Repeat<ImageSource>(hero,20));
            Expect(art.HeroCount == 8, "Artwork memory bounded to eight heroes");
            var heroDrawing=Draw(art);
            var wedges=Walk(heroDrawing).OfType<GeometryDrawing>().Where(d => d.Brush is ImageBrush).ToArray();
            Expect(wedges.Length == 8,"Eight real-image wheel segments");
            foreach(var wedge in wedges)
            {
                Expect(wedge.Geometry is StreamGeometry && wedge.Geometry.IsFrozen,"Precise frozen wedge clipping geometry");
                Expect(((ImageBrush)wedge.Brush).Stretch == Stretch.UniformToFill && ((ImageBrush)wedge.Brush).ImageSource == hero,"Game hero stays upright and covers its wedge");
            }
            art.SetHeroes(new ImageSource[]{null,hero});
            Expect(art.HeroCount == 1 && Walk(Draw(art)).OfType<GeometryDrawing>().Count(d => d.Brush is ImageBrush) == 8,"Small libraries repeat available heroes safely");
            page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent,page));
            Expect(art.HeroCount == 0,"Unload releases decoded hero images");
            var header=(ContentControl)page.FindName("FixedHeaderHost");
            Expect(header.MinHeight == 76 && header.MaxHeight == 76 && header.Content is FrameworkElement,"Same native top-panel shell as HLTB");
            foreach(var size in new[]{new Size(800,600),new Size(1500,900)})
            {
                page.Measure(size); page.Arrange(new Rect(size)); page.UpdateLayout();
                Expect(page.RenderSize == size,"Narrow/wide page layouts");
            }
            if(args.Length == 2)
            {
                XNamespace p="http://schemas.microsoft.com/winfx/2006/xaml/presentation", x="http://schemas.microsoft.com/winfx/2006/xaml";
                var source=XDocument.Load(Path.Combine(args[0],"ExtensionActions.xaml"));
                var theme=XDocument.Load(args[1]);
                foreach(var key in new[]{"OsirisExtensionBrowsePrimaryButtonStyle","OsirisExtensionBrowseInstallPrimaryButtonStyle"}) {
                var actual=source.Descendants(p+"Style").Single(n=>(string)n.Attribute(x+"Key")==key);
                var expected=theme.Descendants(p+"Style").Single(n=>(string)n.Attribute(x+"Key")==key);
                var a=new XElement(actual); var b=new XElement(expected);
                foreach(var n in a.DescendantsAndSelf().Concat(b.DescendantsAndSelf())) n.Attributes().Where(attr=>attr.IsNamespaceDeclaration).Remove();
                Expect(XNode.DeepEquals(a,b),"Bundled action style exactly equals authoritative Extensions style");
                }
                var markup=XDocument.Load(Path.Combine(args[0],"FunGamesPage.xaml"));
                var button=markup.Descendants(p+"Button").Single();
                Expect((string)button.Attribute("Click")=="OnPlayRoulette" && button.Attribute("Command")==null,"Play markup opens Roulette without launching games");
            }
            Expect(!typeof(FunGamesPlugin).GetMethods(BindingFlags.DeclaredOnly|BindingFlags.Public|BindingFlags.Instance).Any(m=>m.Name.StartsWith("OnGame")||m.Name=="GetGameViewControl"),"No tracking or launch hooks");
            RouletteValidation.Run(Expect);
            Console.WriteLine("PASS Fun Games checks="+checks); return 0;
        }
        catch(Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
