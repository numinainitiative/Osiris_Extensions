using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Playnite.SDK.Models;

internal static class Program
{
    private static int checks;
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static void Check(bool value, string name)
    {
        if (!value) throw new InvalidOperationException(name);
        checks++;
    }

    // Models the native two-way selection rebinding when switching from an
    // extension control to Details. No live API, database or visible window.
    public sealed class Entry
    {
        public Game Game { get; set; }
        public Guid Id => Game.Id;
    }
    public sealed class GamesFixture { public ICollectionView CollectionView { get; set; } }
    public sealed class SideItemFixture { public string AppView => "Library"; }
    public sealed class SidebarFixture
    {
        public SideItemFixture SideItem { get; } = new SideItemFixture();
        public ICommand Command { get; set; }
    }
    public sealed class CommandFixture : ICommand
    {
        private readonly Action action;
        public CommandFixture(Action value) { action=value; }
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => action();
        public event EventHandler CanExecuteChanged { add { } remove { } }
    }
    public sealed class ModelFixture : INotifyPropertyChanged
    {
        private IList<object> selected;
        public GamesFixture GamesView { get; set; }
        public List<SidebarFixture> SidebarItems { get; set; }
        public ICommand SwitchDetailsViewCommand { get; set; }
        public ICommand SwitchGridViewCommand { get; set; }
        public bool LibraryActive { get; set; }
        public bool DetailsActive { get; set; }
        public int RebindCount { get; set; }
        public int ExtensionOpenCount { get; set; }
        public Entry SelectedGameDetails => selected?.OfType<Entry>().FirstOrDefault();
        public IList<object> SelectedGamesBinder
        {
            get => selected;
            set { selected=value; PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(SelectedGameDetails))); }
        }
        public void SelectGame(Guid id, bool restoreView)
        {
            SelectedGamesBinder=GamesView.CollectionView.Cast<Entry>().Where(e=>e.Id==id).Cast<object>().ToList();
        }
        public event PropertyChangedEventHandler PropertyChanged;
    }

    private static void DrainBindings()
    {
        var frame=new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>frame.Continue=false));
        Dispatcher.PushFrame(frame);
    }

    [STAThread] private static int Main(string[] args)
    {
        if (args.Length<4) return 2;
        var appRoot=Path.GetFullPath(args[1]);
        AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>
        {
            var file=Path.Combine(appRoot,new AssemblyName(e.Name).Name+".dll");
            return File.Exists(file) ? Assembly.LoadFrom(file) : null;
        };
        try
        {
            var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
            var theme=Assembly.LoadFrom(Path.GetFullPath(args[0]));
            var navigation=theme.GetType("OsirisTheme.HeaderNavigation",true);
            var baseline=args.Contains("--expect-blank");
            var backBaseline=args.Contains("--expect-back-blank");
            foreach(var pair in new[] { new[]{args[2],"Exophase"},new[]{args[3],"HowLongToBeat"} })
            {
                var extension=Assembly.LoadFrom(Path.GetFullPath(pair[0]));
                var prefix="Osiris.Extensions."+pair[1]+".";
                var pageType=extension.GetType(prefix+pair[1]+"GlobalPageControl",true);
                var rowType=extension.GetType(prefix+pair[1]+"GameRow",true);
                var game=new Game { Id=Guid.NewGuid(),Name=pair[1]+" clicked game" };
                var other=new Game { Id=Guid.NewGuid(),Name="Previously selected game" };
                var entries=new List<Entry> { new Entry { Game=other },new Entry { Game=game } };
                var model=new ModelFixture { GamesView=new GamesFixture { CollectionView=CollectionViewSource.GetDefaultView(entries) } };
                var owner=new Window { DataContext=model };
                app.MainWindow=owner;
                var details=new TextBlock { DataContext=model };
                details.SetBinding(TextBlock.TextProperty,new Binding("SelectedGameDetails.Game.Name"));
                model.SidebarItems=new List<SidebarFixture> { new SidebarFixture {
                    Command=new CommandFixture(()=> { model.LibraryActive=true; owner.Content=details; })
                } };
                model.SwitchGridViewCommand=new CommandFixture(()=>model.DetailsActive=false);
                model.SwitchDetailsViewCommand=new CommandFixture(()=>
                {
                    model.DetailsActive=true;
                    model.SelectedGamesBinder=null;
                    owner.Dispatcher.BeginInvoke(DispatcherPriority.DataBind,new Action(()=>
                    {
                        model.RebindCount++;
                        model.SelectedGamesBinder=null;
                    }));
                });
                var constructor=rowType.GetConstructors().Single(c=>c.GetParameters()[0].ParameterType==typeof(Game) && c.GetParameters().Length>5);
                var rowArgs=constructor.GetParameters().Select(p=>p.ParameterType==typeof(Game) ? (object)game :
                    p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null).ToArray();
                var row=constructor.Invoke(rowArgs);
                var page=(UserControl)Activator.CreateInstance(pageType);
                model.SelectGame(other.Id,false);
                var nativePageCommand=new CommandFixture(()=>
                {
                    model.LibraryActive=false;
                    model.ExtensionOpenCount++;
                    owner.Content=page;
                });
                var globalCommandType=theme.GetType("OsirisTheme.ExtensionGlobalPageNavigation+GlobalPageCommand",true);
                var openPage=(ICommand)Activator.CreateInstance(globalCommandType,Hidden,null,
                    new object[]{new ListBox {DataContext=model},pair[1],nativePageCommand,pair[1]+"_fixture",false},null);
                openPage.Execute(null);
                Check(ReferenceEquals(owner.Content,page) && model.ExtensionOpenCount==1,pair[1]+": actual global-page command opens source page once");
                var clickArgs=new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent);
                pageType.GetMethod("OnGameTitleClick",Hidden).Invoke(page,new object[]{new Button {DataContext=row},clickArgs});
                DrainBindings();
                Check(clickArgs.Handled,pair[1]+": title click handled");
                Check(model.LibraryActive && model.DetailsActive,pair[1]+": native library and Details activated");
                Check(model.RebindCount==1,pair[1]+": native selection rebinding exercised");
                Check(Window.GetWindow(page)==null,pair[1]+": source extension page detached");
                if (baseline)
                {
                    Check(model.SelectedGameDetails==null && string.IsNullOrEmpty(details.Text),pair[1]+": baseline blank Details reproduced");
                    Console.WriteLine("REPRODUCED: "+pair[1]+" title changes but Details selection is empty after rebinding.");
                }
                else
                {
                    Check(model.SelectedGameDetails?.Id==game.Id,pair[1]+": clicked game survives native rebinding");
                    Check(details.Text==game.Name,pair[1]+": Details binding shows clicked game, not blank or previous game");
                    navigation.GetMethod("NavigateBack").Invoke(null,null);
                    DrainBindings();
                    var current=navigation.GetField("current",Hidden).GetValue(null);
                    Check((string)current.GetType().GetField("Title").GetValue(current)==pair[1],pair[1]+": Back retains originating extension page");
                    if (backBaseline)
                    {
                        Check(ReferenceEquals(owner.Content,details) && model.ExtensionOpenCount==1,pair[1]+": baseline Back leaves native Details active");
                        Console.WriteLine("REPRODUCED: "+pair[1]+" Back changes heading but does not reopen extension content.");
                    }
                    else
                    {
                        Check(ReferenceEquals(owner.Content,page) && model.ExtensionOpenCount==2 && !model.LibraryActive,pair[1]+": Back reopens actual originating extension content");
                        var savedHistoryCount=((System.Collections.ICollection)navigation.GetField("History",Hidden).GetValue(null)).Count;
                        pageType.GetMethod("OnGameTitleClick",Hidden).Invoke(page,new object[]{new Button {DataContext=row},new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)});
                        navigation.GetMethod("NavigateBack").Invoke(null,null);
                        DrainBindings();
                        Check(ReferenceEquals(owner.Content,page) && model.ExtensionOpenCount==3 && model.SelectedGameDetails==null,pair[1]+": immediate Back remains on extension after pending Details bindings");
                        Check(((System.Collections.ICollection)navigation.GetField("History",Hidden).GetValue(null)).Count==savedHistoryCount,pair[1]+": repeated Back does not add history entries");
                    }

                    // The deferred restore must not overwrite a newer navigation.
                    owner.Content=page;
                    pageType.GetMethod("OnGameTitleClick",Hidden).Invoke(page,new object[]{new Button {DataContext=row},new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)});
                    navigation.GetMethod("NavigateStartingPage",Hidden).Invoke(null,new object[]{model,"Home"});
                    DrainBindings();
                    Check(model.SelectedGameDetails==null,pair[1]+": pending restore ignored after leaving Details");

                    owner.Content=page;
                    pageType.GetMethod("OnGameTitleClick",Hidden).Invoke(page,new object[]{new Button {DataContext=row},new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)});
                    var otherRowArgs=(object[])rowArgs.Clone();
                    otherRowArgs[0]=other;
                    var otherRow=constructor.Invoke(otherRowArgs);
                    pageType.GetMethod("OnGameTitleClick",Hidden).Invoke(page,new object[]{new Button {DataContext=otherRow},new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)});
                    DrainBindings();
                    Check(model.SelectedGameDetails?.Id==other.Id && details.Text==other.Name,pair[1]+": rapid second selection wins over stale restore");
                    Check(model.RebindCount==(backBaseline ? 4 : 5),pair[1]+": both rapid switches exercise native rebinding");

                    var invalidClick=new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent);
                    pageType.GetMethod("OnGameTitleClick",Hidden).Invoke(page,new object[]{new Button(),invalidClick});
                    DrainBindings();
                    Check(!invalidClick.Handled && model.SelectedGameDetails?.Id==other.Id,pair[1]+": missing row leaves current Details untouched");
                }
                owner.Content=null;
                owner.Close();
                DrainBindings();
            }
            Console.WriteLine("PASS "+checks+" extension navigation checks; no visible windows, live data writes or network requests.");
            return 0;
        }
        catch(Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
