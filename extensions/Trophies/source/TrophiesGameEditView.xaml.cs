using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Playnite.SDK.Models;
namespace Osiris.Extensions.Trophies
{
 public partial class TrophiesGameEditView : UserControl
 {
  private readonly TrophiesPlugin plugin;
  private readonly Guid gameId;
  private readonly Game game;
  private Catalogue staged;
  private CancellationTokenSource pending;
  private int generation;
  internal TrophiesGameEditView(TrophiesPlugin plugin,Game game)
  {
   this.plugin=plugin; this.game=game; gameId=game.Id; InitializeComponent();
   var value=plugin.Catalogues.Load(gameId);
   AppIdBox.Text=value?.AppId.ToString() ?? TrophiesViewControl.GuessSteamId(game);
   StatusText.Text=value==null ? "No catalogue imported yet." : value.Trophies.Count+" achievements in the catalogue.";
   SyncHint.Text=TrophySources.IsLocal(game) ? "Uses the edition already saved in Exophase. Sync updates unlocked trophies immediately." : "Sync updates unlocked trophies from this game's connected library.";
   Unloaded+=(s,e)=> { generation++; pending?.Cancel(); SetBusy(false); };
  }
  public void SaveForOsiris(Guid id)
  {
   if (id!=gameId) throw new InvalidOperationException("Trophies editor game mismatch.");
   generation++; pending?.Cancel();
   if (staged!=null) { plugin.Catalogues.Save(gameId,staged); staged=null; }
  }
  private void SetBusy(bool busy) { ImportButton.IsEnabled=!busy; SyncButton.IsEnabled=!busy; }
  private async void ImportClick(object sender,RoutedEventArgs e)
  {
   var request=++generation; pending?.Cancel();
   var operation=new CancellationTokenSource(TimeSpan.FromMinutes(5)); pending=operation; SetBusy(true);
   try
   {
    var app=CatalogueService.ParseAppId(AppIdBox.Text); StatusText.Text="Fetching catalogue…";
    var value=await plugin.Catalogues.Import(gameId,app,plugin.Settings.ApiKey,operation.Token,false);
    if (request==generation) { staged=value; StatusText.Text=value.Trophies.Count+" achievements. Save the game to apply this catalogue."; }
   }
   catch (OperationCanceledException) { if (request==generation) StatusText.Text="Fetch cancelled. Catalogue unchanged."; }
   catch (ArgumentException error) { if (request==generation) StatusText.Text=error.Message; }
   catch { if (request==generation) StatusText.Text="Could not fetch. Check your saved Steam API key and App ID."; }
   finally { operation.Dispose(); if (pending==operation) pending=null; if (request==generation) SetBusy(false); }
  }
  private async void SyncClick(object sender,RoutedEventArgs e)
  {
   var request=++generation; pending?.Cancel();
   var operation=new CancellationTokenSource(TimeSpan.FromMinutes(3)); pending=operation; SetBusy(true);
   try
   {
    if (!plugin.Settings.Committed.Enabled) throw new InvalidDataException("Enable Trophies in extension settings first.");
    if (staged!=null || plugin.Catalogues.Load(gameId)==null) throw new InvalidDataException("Save the game's catalogue first, then Sync.");
    SyncStatus.Text="Syncing trophies…";
    await plugin.SyncGame(gameId,operation.Token);
    if (request==generation) { var value=plugin.LoadForGame(game); SyncStatus.Text=TrophySources.IsLocal(game) ? plugin.Settings.ExophaseStatus : value.Trophies.Count(t=>t.UnlockedUtc.HasValue)+"/"+value.Trophies.Count+" trophies unlocked. Sync complete."; }
   }
   catch (OperationCanceledException) { if (request==generation) SyncStatus.Text="Sync cancelled. Existing trophies unchanged."; }
   catch (InvalidDataException error) { if (request==generation) SyncStatus.Text=error.Message; }
   catch { if (request==generation) SyncStatus.Text="Could not sync. Refresh your Exophase profile and try again."; }
   finally { operation.Dispose(); if (pending==operation) pending=null; if (request==generation) SetBusy(false); }
  }
 }
}
