using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using Osiris.Extensions.FunGames;

internal static class RouletteValidation
{
    static System.Collections.Generic.IEnumerable<Drawing> Drawings(Drawing drawing)
    {
        yield return drawing;
        var group=drawing as DrawingGroup;
        if(group!=null)foreach(var child in group.Children)foreach(var item in Drawings(child))yield return item;
    }
    static object Field(object o,string name)=>o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
    static void Click(RoulettePage page,string name)
    {
        var item=page.FindName(name)??Field(page,name);
        var menu=item as MenuItem;
        if(menu!=null)menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));else ((Button)item).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }
    static RouletteDefinition Sample(string name="Test roulette")=>new RouletteDefinition {Name=name,Options=new System.Collections.Generic.List<RouletteOption>{new RouletteOption{Name="First",Description="First description"},new RouletteOption{Name="Second",Description="Second description"}}};
    internal static void Run(Action<bool,string> check)
    {
        var folder=Path.Combine(Path.GetTempPath(),"osiris-roulette-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try
        {
            var store=new RouletteStore(folder);var file=Path.Combine(folder,"roulettes.json");
            check(store.Load().Count==0 && !File.Exists(file),"Empty store is read-only until explicit save");
            var roulette=Sample();store.Save(roulette);
            var saved=store.Load().Single();check(saved.Id==roulette.Id && saved.Name==roulette.Name && saved.Options[1].Description=="Second description","Named roulette descriptions round-trip");
            var copy=roulette.Clone();copy.Options[0].Name="Draft only";check(roulette.Options[0].Name=="First","Builder uses deep cloned options");
            roulette.Options[0].Description="Updated";store.Save(roulette);
            check(store.Load().Count==1 && store.Load()[0].Options[0].Description=="Updated" && File.Exists(file+".bak"),"Edit updates stable ID atomically and retains backup");
            var original=File.ReadAllText(file);bool rejected=false;
            try{store.Save(Sample(" TEST ROULETTE "));}catch(ArgumentException){rejected=true;}
            check(rejected && File.ReadAllText(file)==original,"Duplicate names rejected without data change");
            var invalid=Sample();invalid.Options.RemoveAt(0);check(invalid.Validate()!=null,"Fewer than two options rejected");
            invalid=Sample();invalid.Options.AddRange(Enumerable.Range(0,31).Select(i=>new RouletteOption{Name="Option"+i}));check(invalid.Validate()!=null,"More than32 options rejected");
            invalid=Sample();invalid.Options[0].Name=" ";check(invalid.Validate()!=null,"Blank option names rejected");
            invalid=Sample();invalid.Name=new string('x',81);check(invalid.Validate()!=null,"Long roulette names rejected");
            invalid=Sample();invalid.Options[0].Description=new string('x',1001);check(invalid.Validate()!=null,"Long descriptions rejected");
            var fallback=RoulettePage.CreateDefault(null);check(fallback.Id==Guid.Empty && fallback.Options.Count==8 && fallback.Validate()==null,"Default has exactly eight usable choices");
            check(fallback.Options.All(o=>!o.GameId.HasValue && o.Description.StartsWith("Sample option")),"Empty library samples never pretend to be owned games");
            var defaultCopy=fallback.Clone();store.Save(defaultCopy);check(defaultCopy.Id!=Guid.Empty && store.Load().Count==2,"Editing default saves a custom copy, not transient default ID");
            for(int count=2;count<=32;count++)
            {
                for(int index=0;index<count;index++)
                {
                    double current=1234.56;var landing=RouletteRandom.LandingPosition(index,count,current);
                    check(Math.Abs(landing%count-index)<0.00001,"Reel stops with selected option exactly beneath centre marker");
                    check(landing>=current+Math.Max(30,count*5),"Reel scrolls at least30 cards and five full cycles");
                }
            }
            check(Enumerable.Range(0,1000).Select(i=>RouletteRandom.Choose(8)).All(i=>i>=0 && i<8),"Random sector choice always in range");
            var page=new RoulettePage(null,folder);var list=(ComboBox)page.FindName("SavedList");
            check(list.Items.Count==3,"Saved roulettes appear alongside default");
            var wheel=(RouletteWheel)page.FindName("Wheel");
            check(list.Parent==page.FindName("OptionsPanel") && Grid.GetRow((UIElement)wheel.Parent)==1,"Selector is in top options panel and reel is centred below");
            check(((StackPanel)page.FindName("InlineResult")).Visibility==Visibility.Collapsed,"Inline result hidden while winner popup remains available");
            check(((Button)page.FindName("ManageButton")).ContextMenu.Items.Count==5,"Manage menu exposes Add/Edit/Delete/Import/Export");
            var portable=Sample("Portable roulette");portable.Options[0].GameId=Guid.NewGuid();
            var exported=RouletteTransfer.Export(portable);var imported=RouletteTransfer.Import(exported);
            check(imported.Id!=portable.Id && imported.Name==portable.Name && imported.Options[1].Description==portable.Options[1].Description,"Portable roulette round-trips names and descriptions with fresh identity");
            check(!exported.Contains("GameId") && !exported.Contains(portable.Id.ToString()) && imported.Options.All(o=>!o.GameId.HasValue),"Export excludes local game and roulette IDs");
            foreach(var bad in new[]{"", "{}", "null", "{\"SchemaVersion\":2,\"Name\":\"Bad\",\"Options\":[]}", exported.Replace("\"SchemaVersion\": 1","\"SchemaVersion\": 9"),new string('x',1024*1024+1)})
            {
                var invalidImport=false;try{RouletteTransfer.Import(bad);}catch{invalidImport=true;}
                check(invalidImport,"Invalid or oversized roulette imports are rejected");
            }
            check(page.FindName("BackButton")==null,"Back to games removed from main page");
            wheel.Measure(new Size(700,640));wheel.Arrange(new Rect(0,0,700,640));
            var visual=new DrawingVisual();
            using(var dc=visual.RenderOpen()) typeof(RouletteWheel).GetMethod("OnRender",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(wheel,new object[]{dc});
            check(visual.Drawing.Bounds.Width>400 && visual.Drawing.Bounds.Height>100 && visual.Drawing.Bounds.Height<400,"Horizontal reel occupies a bounded landscape-card band even at narrow widths");
            check(!Drawings(visual.Drawing).OfType<ImageDrawing>().Any() && !Drawings(visual.Drawing).OfType<GeometryDrawing>().Any(g=>g.Brush is ImageBrush),"Horizontal reel is title-only with no wheel hub or images");
            var shades=Drawings(visual.Drawing).OfType<GeometryDrawing>().Select(g=>g.Brush).OfType<LinearGradientBrush>().Where(b=>b.GradientStops.Count==6).ToArray();
            check(shades.Length==1 && shades[0].GradientStops[0].Color.A>200 && shades[0].GradientStops[5].Color.A>200,"Symmetric side shadows dim outer options");
            check(shades[0].GradientStops[2].Color.A==0 && shades[0].GradientStops[3].Color.A==0,"Winning centre stays clear between side fades");
            var atmosphere=(RouletteAtmosphere)page.FindName("LightShafts");
            check(page.FindName("RouletteFrame")==null,"Background window/frame removed while shafts remain");
            check(Drawings(visual.Drawing).OfType<GeometryDrawing>().Any(g=>g.Pen?.Brush is SolidColorBrush && ((SolidColorBrush)g.Pen.Brush).Color==Colors.White),"Fixed centre marker has visible white edge");
            check(!atmosphere.IsHitTestVisible,"Light shafts cannot capture clicks");
            atmosphere.Measure(new Size(800,600));atmosphere.Arrange(new Rect(0,0,800,600));var lightVisual=new DrawingVisual();
            using(var dc=lightVisual.RenderOpen())typeof(RouletteAtmosphere).GetMethod("OnRender",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(atmosphere,new object[]{dc});
            check(Drawings(lightVisual.Drawing).OfType<GeometryDrawing>().Count()==7,"Static atmosphere uses two glows and five soft light shafts");
            check(Drawings(lightVisual.Drawing).OfType<DrawingGroup>().Any(g=>g.OpacityMask is RadialGradientBrush),"Light shafts fade away into background in every direction");
            var rotations=Drawings(visual.Drawing).OfType<DrawingGroup>().Select(g=>g.Transform).OfType<RotateTransform>().Select(t=>t.Angle).ToArray();
            check(rotations.Length==0,"Reel titles stay horizontal");
            check(page.FindName("WheelTitle")==null && wheel.MaxWidth==1400 && wheel.Height==400 && wheel.MaxHeight==480,"Larger horizontal reel replaces circular layout");
            var cards=Drawings(visual.Drawing).OfType<GeometryDrawing>().Select(g=>g.Geometry).OfType<RectangleGeometry>().Where(g=>g.RadiusX==10).ToArray();
            check(cards.Length>2 && cards.All(g=>g.Rect.Width/g.Rect.Height>=1.59),"Reel cards retain landscape proportions for longer titles");
            page.Measure(new Size(1280,900));page.Arrange(new Rect(0,0,1280,900));page.UpdateLayout();
            var spin=(Button)page.FindName("SpinButton");var parent=(UIElement)page.FindName("PlayView");
            var wheelPoint=wheel.TranslatePoint(new Point(wheel.ActualWidth,0),parent);
            var buttonPoint=spin.TranslatePoint(new Point(spin.ActualWidth,0),parent);
            check(Math.Abs(wheelPoint.X-buttonPoint.X)<1,"Spin button aligns with right edge of centred reel");
            var reelCentre=wheel.TranslatePoint(new Point(wheel.ActualWidth/2,0),parent);
            check(Math.Abs(reelCentre.X-((Grid)parent).ActualWidth/2)<1,"Reel is horizontally centred across page");
            var slotMethod=typeof(RouletteWheel).GetMethod("OptionAtSlot",BindingFlags.Static|BindingFlags.NonPublic);
            foreach(var count in new[]{2,8,32})
                for(var slot=-40;slot<=40;slot++)
                    check((int)slotMethod.Invoke(null,new object[]{slot,count})==(slot%count+count)%count,"Reel repeats choices correctly on both sides");
            Click(page,"BuildButton");
            var rows=(ObservableCollection<RouletteOption>)Field(page,"draftOptions");
            check(rows.Count==2 && ((Grid)page.FindName("BuilderView")).Visibility==Visibility.Visible,"Build opens two editable options");
            Click(page,"AddOptionButton");check(rows.Count==3,"Builder adds option");
            var beforeCancel=File.ReadAllText(file);Click(page,"CancelButton");check(File.ReadAllText(file)==beforeCancel,"Cancel never persists draft");
            Click(page,"BuildButton");check(page.TryNavigateBack() && ((Grid)page.FindName("BuilderView")).Visibility==Visibility.Collapsed,"Search-panel Back cancels builder draft first");
            Click(page,"BuildButton");Click(page,"SaveButton");check(((Grid)page.FindName("BuilderView")).Visibility==Visibility.Visible && !string.IsNullOrWhiteSpace(((TextBlock)page.FindName("BuilderStatus")).Text),"Invalid builder remains open with inline error");
            ((TextBox)page.FindName("RouletteName")).Text="Saved from builder";rows=(ObservableCollection<RouletteOption>)Field(page,"draftOptions");rows[0].Name="One";rows[0].Description="First detail";rows[1].Name="Two";rows[1].Description="Second detail";
            Click(page,"SaveButton");check(store.Load().Any(r=>r.Name=="Saved from builder" && r.Options[0].Description=="First detail"),"Builder persists option names and descriptions");
            check(((Grid)page.FindName("BuilderView")).Visibility==Visibility.Collapsed && ((RouletteDefinition)list.SelectedItem).Name=="Saved from builder","Saved roulette selected and ready to spin");
            Click(page,"EditButton");
            page.Measure(new Size(1280,900));page.Arrange(new Rect(0,0,1280,900));page.UpdateLayout();
            var rowsControl=(ItemsControl)page.FindName("OptionRows");
            check(rowsControl.Items.Count==2 && ((TextBox)page.FindName("RouletteName")).ActualHeight==40,"Actual builder templates measure with canonical input height");
            Click(page,"CancelButton");
            Click(page,"EditButton");rows=(ObservableCollection<RouletteOption>)Field(page,"draftOptions");rows[0].Description="Edited description";Click(page,"SaveButton");
            check(store.Load().Count(r=>r.Name=="Saved from builder")==1 && store.Load().Single(r=>r.Name=="Saved from builder").Options[0].Description=="Edited description","Edit existing saved roulette preserves ID and replaces content");
            Click(page,"SpinButton");check(wheel.IsSpinning && !list.IsEnabled && !((Button)page.FindName("SpinButton")).IsEnabled && !((MenuItem)Field(page,"BuildButton")).IsEnabled,"Spin locks changes/double spins");
            check(!wheel.Spin(),"Double-spin rejected");
            wheel.Measure(new Size(900,320));wheel.Arrange(new Rect(0,0,900,320));
            check(wheel.IsSpinning,"Resizing the horizontal reel does not interrupt its spin");
            var frame=new DispatcherFrame();var timer=new DispatcherTimer {Interval=TimeSpan.FromSeconds(5.7)};
            timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);
            var result=((TextBlock)page.FindName("ResultName")).Text;
            check(!wheel.IsSpinning && (result=="One"||result=="Two"),"Real animation completes and reports winner");
            var popup=(Grid)page.FindName("WinnerOverlay");var confetti=(WinnerConfetti)page.FindName("Confetti");
            check(popup.Visibility==Visibility.Visible && !list.IsEnabled,"Winner popup appears on completion and blocks underlying roulette");
            check(((TextBlock)page.FindName("WinnerName")).Text==result && ((TextBlock)page.FindName("WinnerDescription")).Text==(result=="One"?"Edited description":"Second detail"),"Popup shows actual winning name and description");
            check(confetti.IsRunning && !confetti.IsHitTestVisible && popup.Children[0]==confetti && popup.Children[1] is Border,"Confetti runs behind winner card without capturing clicks");
            check(System.Windows.Input.KeyboardNavigation.GetTabNavigation(popup)==System.Windows.Input.KeyboardNavigationMode.Cycle,"Keyboard focus stays within open winner popup");
            var celebrationFrame=new DispatcherFrame();var celebrationTimer=new DispatcherTimer {Interval=TimeSpan.FromSeconds(2.9)};
            celebrationTimer.Tick+=(s,e)=>{celebrationTimer.Stop();celebrationFrame.Continue=false;};celebrationTimer.Start();Dispatcher.PushFrame(celebrationFrame);
            check(!confetti.IsRunning && confetti.Progress==1 && popup.Visibility==Visibility.Visible,"Confetti stops automatically while winner remains readable");
            Click(page,"WinnerCloseButton");check(popup.Visibility==Visibility.Collapsed && list.IsEnabled,"Close dismisses result and restores roulette controls");
            check(((TextBlock)page.FindName("ResultDescription")).Text==(result=="One"?"Edited description":"Second detail"),"Result includes winning description");
            var winnerIndex=result=="One"?0:1;check(Math.Abs(wheel.Position-winnerIndex)<0.0001,"Visible centre card matches reported winner");
            typeof(RoulettePage).GetMethod("OnSpinCompleted",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(page,new object[]{new RouletteOption{Name="Another winner",Description="Details"}});
            check(page.TryNavigateBack() && popup.Visibility==Visibility.Collapsed && !confetti.IsRunning,"Search-panel Back dismisses celebration before page navigation");
            typeof(RoulettePage).GetMethod("OnSpinCompleted",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(page,new object[]{new RouletteOption{Name="Winner",Description=""}});
            page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent,page));
            check(popup.Visibility==Visibility.Collapsed && !confetti.IsRunning,"Leaving page clears popup and animation");
            // Standalone drawing inspection, no window or screenshots.
            var particles=new WinnerConfetti();particles.Measure(new Size(800,600));particles.Arrange(new Rect(0,0,800,600));particles.Progress=0.35;
            var particleVisual=new DrawingVisual();using(var dc=particleVisual.RenderOpen())typeof(WinnerConfetti).GetMethod("OnRender",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(particles,new object[]{dc});
            check(Drawings(particleVisual.Drawing).OfType<GeometryDrawing>().Count()==64,"Confetti renderer has a fixed64-particle budget");
            particles.Start();particles.Stop();check(!particles.IsRunning && particles.Progress==1,"Animation can be stopped and cleaned immediately");
            Click(page,"SpinButton");page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent,page));check(!wheel.IsSpinning,"Navigation cancels active animation");
            var toDelete=store.Load().First(r=>r.Name=="Saved from builder");
            var deletePage=new RoulettePage(null,folder);var deleteList=(ComboBox)deletePage.FindName("SavedList");
            deleteList.SelectedItem=deleteList.Items.Cast<RouletteDefinition>().First(r=>r.Id==toDelete.Id);
            Click(deletePage,"DeleteButton");check(((Grid)deletePage.FindName("DeleteOverlay")).Visibility==Visibility.Visible,"Delete requires confirmation");
            Click(deletePage,"CancelDeleteButton");check(store.Load().Any(r=>r.Id==toDelete.Id),"Cancelled deletion preserves saved roulette");
            Click(deletePage,"DeleteButton");Click(deletePage,"ConfirmDeleteButton");
            check(!store.Load().Any(r=>r.Id==toDelete.Id) && File.Exists(file+".bak"),"Confirmed deletion removes only selected save and keeps backup");
            check(((RouletteDefinition)deleteList.SelectedItem).Id==Guid.Empty,"Delete returns selector to default");
            bool defaultProtected=false;try{store.Delete(Guid.Empty);}catch(ArgumentException){defaultProtected=true;}check(defaultProtected,"Default roulette cannot be deleted");
            File.WriteAllText(file,"invalid JSON");var corrupt=new RouletteStore(folder);bool failed=false;try{corrupt.Load();}catch{failed=true;}
            check(failed,"Corrupt save is detected");failed=false;try{corrupt.Save(Sample());}catch(IOException){failed=true;}
            check(failed && File.ReadAllText(file)=="invalid JSON","Corrupt user file never silently overwritten");
            var blockedPage=new RoulettePage(null,folder);check(!((Button)blockedPage.FindName("ManageButton")).IsEnabled,"Page with unreadable saved data disables saving without destroying file");
            var failedSavePage=new RoulettePage(null,Path.Combine(file,"not-a-directory"));Click(failedSavePage,"BuildButton");
            ((TextBox)failedSavePage.FindName("RouletteName")).Text="Cannot save";
            var failedRows=(ObservableCollection<RouletteOption>)Field(failedSavePage,"draftOptions");failedRows[0].Name="A";failedRows[1].Name="B";
            Click(failedSavePage,"SaveButton");check(((Grid)failedSavePage.FindName("BuilderView")).Visibility==Visibility.Visible && ((TextBlock)failedSavePage.FindName("BuilderStatus")).Text.StartsWith("Could not save:"),"I/O failure keeps valid draft open with error");
        }
        finally
        {
            foreach(var name in Directory.GetFiles(folder)) File.Delete(name);
            Directory.Delete(folder);
        }
    }
}
