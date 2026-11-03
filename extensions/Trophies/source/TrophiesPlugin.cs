using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Playnite.SDK;
using Playnite.SDK.Plugins;

namespace Osiris.Extensions.Trophies
{
    public sealed class TrophiesPlugin : GenericPlugin
    {
        public override Guid Id { get; } = Guid.Parse("72ba1fc9-490b-4fa4-96f0-0a154b56c1db");
        internal readonly CatalogueService Catalogues;
        internal readonly TrophiesSettingsModel Settings;
        internal readonly ExophaseSyncService Exophase;
        private readonly SemaphoreSlim syncGate = new SemaphoreSlim(1,1);
        private static readonly ILogger logger = LogManager.GetLogger();
        public TrophiesPlugin(IPlayniteAPI api) : base(api)
        {
            Properties = new GenericPluginProperties { HasSettings = true };
            Settings = new TrophiesSettingsModel(this);
            Catalogues = new CatalogueService(Path.Combine(api.Paths.ExtensionsDataPath, Id.ToString("D")));
            Exophase=new ExophaseSyncService(api);
            Catalogues.ResolveOwner = ResolveTrophiesOwner;
            AddCustomElementSupport(new AddCustomElementSupportArgs { SourceName = "Trophies", ElementList = new List<string> { "TrophiesViewControl" } });
        }
        public override Control GetGameViewControl(GetGameViewControlArgs args) => args.Name == "TrophiesViewControl" ? new TrophiesViewControl(this) : null;
        public override ISettings GetSettings(bool firstRunSettings) => Settings;
        // Read-only bridge for Stats. Use the same source-filtered catalogue as Game Details.
        public string GetEarnedTrophiesForStatsForOsiris(Guid gameId)
        {
            var game = PlayniteApi.Database.Games.Get(gameId);
            var catalogue = game == null ? null : LoadForGame(game);
            return Playnite.SDK.Data.Serialization.ToJson((catalogue?.Trophies ?? new List<Trophy>())
                .Where(t => t.UnlockedUtc.HasValue)
                .Select(t => new { t.Id, t.Name, t.UnlockedUtc, t.GlobalPercent, Icon = t.LocalIcon,
                    GameName = game.Name }).ToList());
        }
        public UserControl GetGameEditControlForOsiris(Guid gameId)
        {
            return GetEditor(gameId, ResolveTrophiesOwner(gameId), false);
        }
        public UserControl GetSharedGameEditControlForOsiris(Guid gameId, Guid sourceId) => GetEditor(gameId, sourceId, true);
        private UserControl GetEditor(Guid target, Guid source, bool preview)
        {
            var game = PlayniteApi.Database.Games.Get(source);
            return game == null ? null : new TrophiesGameEditView(this, game, target, preview);
        }
        internal Guid ResolveTrophiesOwner(Guid id)
        {
            var theme = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "OsirisTheme");
            var method = theme?.GetType("OsirisTheme.EditionPageSharing")?.GetMethod("ResolveOwner");
            return method == null ? id : (Guid)method.Invoke(null, new object[] { id, "Trophies" });
        }
        internal Playnite.SDK.Models.Game EffectiveGame(Playnite.SDK.Models.Game game)
        {
            if (game == null) return null;
            var owner = ResolveTrophiesOwner(game.Id);
            return owner == game.Id ? game : PlayniteApi.Database.Games.Get(owner) ?? game;
        }
        public override UserControl GetSettingsView(bool firstRunSettings) => new TrophiesSettingsView(Settings);
        internal Catalogue LoadForGame(Playnite.SDK.Models.Game game)
        {
            game = EffectiveGame(game);
            var catalogue = game == null ? null : Catalogues.Load(game.Id);
            return TrophySources.ForDisplay(game, catalogue);
        }
        internal async Task SyncGame(Guid gameId, CancellationToken token)
        {
            await syncGate.WaitAsync(token);
            try { await SyncGameCore(ResolveTrophiesOwner(gameId),token); }
            finally { syncGate.Release(); }
        }
        public override async void OnApplicationStarted(Playnite.SDK.Events.OnApplicationStartedEventArgs args)
        {
            try { await SyncIntegratedLibraryForOsiris(CancellationToken.None); }
            catch (Exception error) { logger.Warn("Trophies startup sync unavailable ("+error.GetType().Name+")."); }
        }
        public override async void OnLibraryUpdated(Playnite.SDK.Events.OnLibraryUpdatedEventArgs args)
        {
            try { await SyncIntegratedLibraryForOsiris(CancellationToken.None); }
            catch (Exception error) { logger.Warn("Trophies library sync unavailable ("+error.GetType().Name+")."); }
        }
        public async Task SyncIntegratedLibraryForOsiris(CancellationToken token)
        {
            if (!Settings.Committed.Enabled) return;
            foreach (var game in PlayniteApi.Database.Games.Where(g=>g.PluginId==TrophySources.Steam || g.PluginId==TrophySources.Xbox).ToList())
            {
                token.ThrowIfCancellationRequested();
                if (Catalogues.Load(game.Id)==null) continue;
                using (var operation=CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    operation.CancelAfter(TimeSpan.FromSeconds(45));
                    try { await SyncGame(game.Id,operation.Token); }
                    catch (OperationCanceledException) { if (token.IsCancellationRequested) throw; }
                    catch (InvalidDataException error) { logger.Warn("Trophies native sync: "+error.Message); Settings.ExophaseStatus=error.Message; }
                    catch (Exception error) { logger.Warn("Trophies native sync failed ("+error.GetType().Name+"). Existing unlocks unchanged."); }
                }
            }
        }
        private async Task SyncGameCore(Guid gameId, CancellationToken token)
        {
            var game=PlayniteApi.Database.Games.Get(gameId);
            if (game==null || !Settings.Committed.Enabled) return;
            var catalogue=Catalogues.Load(gameId);
            if (catalogue==null) return;
            if (!TrophySources.IsLocal(game))
            {
                var source=game.PluginId;
                await TrophySources.Fetch(PlayniteApi,Catalogues,game,catalogue,Settings.ApiKey,token);
                token.ThrowIfCancellationRequested();
                var current=PlayniteApi.Database.Games.Get(gameId);
                var latest=Catalogues.Load(gameId);
                if (!Settings.Committed.Enabled || current?.PluginId!=source || latest?.AppId!=catalogue.AppId) return;
                latest.SharedSourcePluginId=null;
                latest.NativeProvider=catalogue.NativeProvider; latest.NativeAccount=catalogue.NativeAccount;
                foreach (var trophy in latest.Trophies) trophy.NativeUnlockedUtc=catalogue.Trophies.Find(t=>t.Id==trophy.Id)?.NativeUnlockedUtc;
                Catalogues.Save(gameId,latest);
                logger.Info("Trophies native sync: "+latest.Trophies.Count(t=>t.NativeUnlockedUtc.HasValue)+"/"+latest.Trophies.Count+" earned; provider "+source+"; game "+gameId+".");
                return;
            }
            var appId = catalogue.AppId;
            var result = await Exophase.FetchForGame(gameId, token);
            token.ThrowIfCancellationRequested();
            if (!TrophySources.IsLocal(PlayniteApi.Database.Games.Get(gameId))) return;
            // Reload before merging so a simultaneous editor Save cannot be overwritten.
            catalogue=Catalogues.Load(gameId);
            if (catalogue==null || catalogue.AppId != appId || !Settings.Committed.Enabled) return;
            foreach (var page in result.Pages) ExophasePageParser.Apply(catalogue, page, result.ProfileId, page.GameTitle, true);
            catalogue.ExophasePageUrl="";
            catalogue.SharedSourcePluginId=null;
            Catalogues.Save(gameId,catalogue);
            Settings.ExophaseStatus=catalogue.Trophies.Count(t=>t.UnlockedUtc.HasValue)+"/"+catalogue.Trophies.Count+" trophies unlocked. " + (result.Warnings?.Count > 0 ? string.Join(" ", result.Warnings) : "Synced from your saved Exophase edition.");
        }
    }
}
