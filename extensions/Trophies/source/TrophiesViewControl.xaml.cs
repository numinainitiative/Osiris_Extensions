using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using System.Threading;
using Playnite.SDK.Controls;
using Playnite.SDK.Models;

namespace Osiris.Extensions.Trophies
{
    public partial class TrophiesViewControl : PluginUserControl
    {
        public static readonly DependencyProperty IsCardVisibleProperty = DependencyProperty.Register(nameof(IsCardVisible), typeof(bool), typeof(TrophiesViewControl), new PropertyMetadata(false));
        public bool IsCardVisible { get => (bool)GetValue(IsCardVisibleProperty); private set => SetValue(IsCardVisibleProperty, value); }
        public static readonly DependencyProperty CountTextProperty = DependencyProperty.Register(nameof(CountText), typeof(string), typeof(TrophiesViewControl), new PropertyMetadata("0/0"));
        public string CountText { get => (string)GetValue(CountTextProperty); private set => SetValue(CountTextProperty, value); }
        private readonly TrophiesPlugin plugin;
        private Game game;
        private readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        private CancellationTokenSource synchronising;
        private Guid lastSyncGame;
        private DateTime lastSyncAttempt;
        private DateTime catalogueRevision;
        private bool lastSyncSucceeded;
        internal TrophiesViewControl(TrophiesPlugin plugin)
        {
            this.plugin = plugin; InitializeComponent();
            Loaded += (s,e) => { Refresh(); timer.Start(); Synchronise(); };
            Unloaded += (s,e) => { timer.Stop(); synchronising?.Cancel(); };
            timer.Tick += (s,e) => { if (game!=null && catalogueRevision!=plugin.Catalogues.Revision(game.Id)) Refresh(); IsCardVisible = game != null && plugin.Settings.Committed.Enabled && TrophyList.Items.Count > 0; Synchronise(); };
        }
        public override void GameContextChanged(Game oldContext, Game newContext) { synchronising?.Cancel(); game = newContext; Refresh(); if (IsLoaded) Synchronise(); }
        private async void Synchronise()
        {
            // Local Exophase updates are explicit: use Sync in the game's editor.
            if (synchronising!=null || game==null || !plugin.Settings.Committed.Enabled || TrophySources.IsLocal(game)) return;
            if (plugin.Catalogues.Load(game.Id)==null) return;
            if (lastSyncGame==game.Id && DateTime.UtcNow-lastSyncAttempt<(lastSyncSucceeded ? TimeSpan.FromMinutes(10) : TimeSpan.FromSeconds(30))) return;
            var id=game.Id; lastSyncGame=id; lastSyncAttempt=DateTime.UtcNow;
            var operation=new CancellationTokenSource(TimeSpan.FromSeconds(45)); synchronising=operation;
            lastSyncSucceeded=false;
            try { await plugin.SyncGame(id,operation.Token); lastSyncSucceeded=true; if (game?.Id==id && IsLoaded) Refresh(); }
            catch (OperationCanceledException) { }
            catch (System.IO.InvalidDataException error) { plugin.Settings.ExophaseStatus=error.Message; }
            catch { plugin.Settings.ExophaseStatus="Library trophy synchronisation unavailable. Existing unlocks unchanged."; }
            finally { synchronising=null; operation.Dispose(); }
        }
        private void Refresh()
        {
            var catalogue = game == null ? null : plugin.LoadForGame(game);
            catalogueRevision=game==null ? default : plugin.Catalogues.Revision(game.Id);
            TrophyList.ItemsSource = catalogue == null ? null : TrophyRow.Preview(catalogue.Trophies);
            CountText = (catalogue?.Trophies.Count(t => t.UnlockedUtc.HasValue) ?? 0) + "/" + (catalogue?.Trophies.Count ?? 0);
            IsCardVisible = game != null && plugin.Settings.Committed.Enabled && catalogue?.Trophies.Count > 0;
        }
        private void ViewAllClick(object sender, RoutedEventArgs e)
        {
            if (game == null) return;
            var catalogue = plugin.LoadForGame(game);
            var owner = Window.GetWindow(this);
            if (catalogue == null || owner == null) return;
            var window = new TrophiesWindow(game.Name, catalogue.Trophies) { Owner = owner };
            var theme = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "OsirisTheme");
            var showDimmer = theme?.GetType("OsirisTheme.MainWindowDimmer")?.GetMethod("Show",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(Window), typeof(Window) }, null);
            IDisposable dimmer = null;
            try
            {
                dimmer = showDimmer?.Invoke(null, new object[] { owner, window }) as IDisposable;
                var typography = theme?.GetType("OsirisTheme.OsirisApplicationFontSize")?.GetMethod("ApplyCurrentTypography", new[] { typeof(DependencyObject) });
                typography?.Invoke(null, new object[] { window });
                window.ShowDialog();
            }
            finally { dimmer?.Dispose(); }
        }
        internal static string GuessSteamId(Game value)
        {
            if (value.PluginId == Guid.Parse("cb91dfc9-b977-43bf-8e70-55f46e410fab") && uint.TryParse(value.GameId, out var id) && id > 0) return id.ToString();
            if (value.Links != null) foreach (var link in value.Links)
            {
                if (Uri.TryCreate(link.Url, UriKind.Absolute, out var uri) && uri.Host == "store.steampowered.com")
                    try { return CatalogueService.ParseAppId(link.Url).ToString(); } catch (ArgumentException) { }
            }
            return "";
        }
    }
}
