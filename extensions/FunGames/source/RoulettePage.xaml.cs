using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using Playnite.SDK;

namespace Osiris.Extensions.FunGames
{
    public sealed partial class RoulettePage : UserControl
    {
        private readonly IPlayniteAPI api;
        private readonly RouletteStore store;
        private readonly ObservableCollection<RouletteOption> draftOptions=new ObservableCollection<RouletteOption>();
        private readonly List<RouletteDefinition> roulettes=new List<RouletteDefinition>();
        private RouletteDefinition selected,draft;
        private bool saveBlocked;
        private IInputElement winnerPreviousFocus;
        private readonly ContextMenu manageMenu=new ContextMenu();
        private readonly MenuItem BuildButton=new MenuItem {Header="Add roulette"};
        private readonly MenuItem EditButton=new MenuItem {Header="Edit roulette"};
        private readonly MenuItem DeleteButton=new MenuItem {Header="Delete roulette"};
        private readonly MenuItem ImportButton=new MenuItem {Header="Import roulette…"};
        private readonly MenuItem ExportButton=new MenuItem {Header="Export selected roulette…"};
        private Guid pendingDelete;
        public RoulettePage() : this(null,null) { }
        public RoulettePage(IPlayniteAPI api,string dataFolder)
        {
            this.api=api; store=string.IsNullOrWhiteSpace(dataFolder)?null:new RouletteStore(dataFolder);
            InitializeComponent(); OptionRows.ItemsSource=draftOptions;
            manageMenu.Background=new SolidColorBrush(Color.FromRgb(17,17,17));manageMenu.BorderBrush=new SolidColorBrush(Color.FromRgb(40,40,40));manageMenu.BorderThickness=new Thickness(1);manageMenu.Padding=new Thickness(4);
            foreach(var item in new[]{BuildButton,EditButton,DeleteButton,ImportButton,ExportButton}) {item.Height=40;item.Padding=new Thickness(14,0,14,0);item.Foreground=new SolidColorBrush(Color.FromRgb(240,241,247));manageMenu.Items.Add(item);}
            BuildButton.Click+=OnBuild;EditButton.Click+=OnEdit;DeleteButton.Click+=OnDelete;
            ImportButton.Click+=OnImport;ExportButton.Click+=OnExport;
            ManageButton.ContextMenu=manageMenu;
            roulettes.Add(CreateDefault(api));
            try { if(store!=null) roulettes.AddRange(store.Load()); }
            catch { saveBlocked=true; PageStatus.Text="Saved roulettes could not be read. Existing data has been preserved; saving is disabled."; }
            SavedList.ItemsSource=roulettes; SavedList.SelectedIndex=0;
            Wheel.SpinCompleted+=OnSpinCompleted;
            Unloaded+=OnUnloaded;
            IsVisibleChanged+=(s,e)=> { if(!(bool)e.NewValue) {CloseWinner(false);CloseDelete();manageMenu.IsOpen=false;if(Wheel.IsSpinning) {Wheel.CancelSpin();SetBusy(false);ResultName.Text="Spin to choose an option";ResultDescription.Text="";} } };
            PreviewKeyDown+=(s,e)=> {if(e.Key==Key.Escape) {if(DeleteOverlay.Visibility==Visibility.Visible){CloseDelete();e.Handled=true;}else if(WinnerOverlay.Visibility==Visibility.Visible){CloseWinner();e.Handled=true;}}};
            SetBusy(false);
        }
        public static RouletteDefinition CreateDefault(IPlayniteAPI api)
        {
            var result=new RouletteDefinition { Id=Guid.Empty, Name="Random games" };
            try
            {
                var games=api?.Database.Games.Where(g=>!g.Hidden && !string.IsNullOrWhiteSpace(g.Name)).ToList();
                if(games!=null)
                {
                    for(var i=games.Count-1;i>0;i--) {var j=RouletteRandom.Choose(i+1);var value=games[i];games[i]=games[j];games[j]=value;}
                    result.Options.AddRange(games.Take(8).Select(g=>new RouletteOption {Name=g.Name.Length>80?g.Name.Substring(0,80):g.Name,Description="From your Osiris library.",GameId=g.Id}));
                }
            }
            catch { }
            // Explicit sample options keep a new/empty library's wheel usable without pretending they are owned games.
            var samples=new[]{"Adventure","Racing","Puzzle","Strategy","Action","Role-playing","Platformer","Simulation"};
            while(result.Options.Count<8) result.Options.Add(new RouletteOption {Name=samples[result.Options.Count],Description="Sample option — build a roulette to replace it with your own choice."});
            return result;
        }
        private void OnSelectionChanged(object sender,SelectionChangedEventArgs args)
        {
            if(Wheel.IsSpinning) return;
            selected=SavedList.SelectedItem as RouletteDefinition;
            if(selected==null) return;
            ResultName.Text="Spin to choose an option"; ResultDescription.Text="";
            Wheel.SetRoulette(selected);
            EditButton.IsEnabled=!saveBlocked;
        }
        private void OnSpin(object sender,RoutedEventArgs args)
        {
            if(WinnerOverlay.Visibility==Visibility.Visible)return;
            if(Wheel.Spin()) { ResultName.Text="Spinning…";ResultDescription.Text="";SetBusy(true); }
        }
        private void OnSpinCompleted(RouletteOption option)
        {
            ResultName.Text=option.Name;ResultDescription.Text=option.Description;SetBusy(false);
            WinnerName.Text=option.Name;WinnerDescription.Text=option.Description??"";
            winnerPreviousFocus=Keyboard.FocusedElement;
            WinnerOverlay.Visibility=Visibility.Visible;PlayView.IsEnabled=false;
            Confetti.Start();WinnerCloseButton.Focus();
        }
        private void OnWinnerClose(object sender,RoutedEventArgs args) {CloseWinner();args.Handled=true;}
        private void CloseWinner(bool restoreFocus=true)
        {
            var wasOpen=WinnerOverlay.Visibility==Visibility.Visible;
            Confetti.Stop();WinnerOverlay.Visibility=Visibility.Collapsed;PlayView.IsEnabled=true;
            if(wasOpen && restoreFocus)
            {
                var previous=winnerPreviousFocus as UIElement;
                if(previous!=null && previous.IsVisible && previous.IsEnabled)previous.Focus();else SpinButton.Focus();
            }
            winnerPreviousFocus=null;
        }
        private void SetBusy(bool busy)
        {
            SavedList.IsEnabled=!busy;SpinButton.IsEnabled=!busy;ManageButton.IsEnabled=!busy && !saveBlocked;
            BuildButton.IsEnabled=!busy && !saveBlocked;EditButton.IsEnabled=!busy && !saveBlocked;
            DeleteButton.IsEnabled=!busy && !saveBlocked && selected!=null && selected.Id!=Guid.Empty;
            ImportButton.IsEnabled=!busy && !saveBlocked && store!=null;
            ExportButton.IsEnabled=!busy && selected!=null;
        }
        private void OnExport(object sender,RoutedEventArgs args)
        {
            if(Wheel.IsSpinning || selected==null)return;
            var safeName=new string(selected.Name.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c).ToArray()).Trim().TrimEnd('.');
            var dialog=new Microsoft.Win32.SaveFileDialog {Title="Export roulette",Filter="Osiris roulette (*.roulette.json)|*.roulette.json",DefaultExt=".roulette.json",AddExtension=true,OverwritePrompt=true,FileName=string.IsNullOrWhiteSpace(safeName)?"Roulette.roulette.json":safeName+".roulette.json"};
            try
            {
                var owner=Window.GetWindow(this);
                if((owner==null?dialog.ShowDialog():dialog.ShowDialog(owner))!=true)return;
                File.WriteAllText(dialog.FileName,RouletteTransfer.Export(selected));PageStatus.Text="Roulette exported.";
            }
            catch(Exception error){PageStatus.Text="Could not export: "+error.Message;}
        }
        private void OnImport(object sender,RoutedEventArgs args)
        {
            if(Wheel.IsSpinning || saveBlocked || store==null)return;
            var dialog=new Microsoft.Win32.OpenFileDialog {Title="Import roulette",Filter="Osiris roulette (*.roulette.json)|*.roulette.json|JSON files (*.json)|*.json",CheckFileExists=true,Multiselect=false};
            try
            {
                var owner=Window.GetWindow(this);
                if((owner==null?dialog.ShowDialog():dialog.ShowDialog(owner))!=true)return;
                if(new FileInfo(dialog.FileName).Length>1024*1024)throw new InvalidDataException("The roulette file is too large (maximum 1 MB).");
                var imported=RouletteTransfer.Import(File.ReadAllText(dialog.FileName));
                OpenBuilder(false);draft=imported;RouletteName.Text=draft.Name;draftOptions.Clear();foreach(var option in draft.Options)draftOptions.Add(option);
                UpdateAddButton();BuilderStatus.Text="Imported roulette — review it and Save to add it. Existing roulettes are not replaced.";
            }
            catch(Exception error){PageStatus.Text="Could not import: "+error.Message;}
        }
        private void OnManage(object sender,RoutedEventArgs args) {SetBusy(Wheel.IsSpinning);manageMenu.FontFamily=FontFamily;manageMenu.FontSize=17;manageMenu.FontWeight=FontWeights.SemiBold;manageMenu.PlacementTarget=ManageButton;manageMenu.IsOpen=true;}
        private void OnDelete(object sender,RoutedEventArgs args)
        {
            if(selected==null || selected.Id==Guid.Empty || Wheel.IsSpinning || saveBlocked)return;
            pendingDelete=selected.Id;DeleteName.Text=selected.Name;DeleteOverlay.Visibility=Visibility.Visible;PlayView.IsEnabled=false;CancelDeleteButton.Focus();
        }
        private void CloseDelete() {DeleteOverlay.Visibility=Visibility.Collapsed;PlayView.IsEnabled=true;pendingDelete=Guid.Empty;}
        private void OnCancelDelete(object sender,RoutedEventArgs args) {CloseDelete();ManageButton.Focus();}
        private void OnConfirmDelete(object sender,RoutedEventArgs args)
        {
            if(pendingDelete==Guid.Empty)return;
            try
            {
                if(store==null)throw new IOException("Saving is unavailable outside the extension host.");
                store.Delete(pendingDelete);roulettes.RemoveAll(r=>r.Id==pendingDelete);SavedList.Items.Refresh();SavedList.SelectedIndex=0;CloseDelete();PageStatus.Text="Roulette deleted. Previous data is retained in the backup.";
            }
            catch(Exception error) {DeleteName.Text="Could not delete: "+error.Message;}
        }
        public bool TryNavigateBack()
        {
            if(DeleteOverlay.Visibility==Visibility.Visible){CloseDelete();return true;}
            if(WinnerOverlay.Visibility==Visibility.Visible){CloseWinner();return true;}
            if(BuilderView.Visibility!=Visibility.Visible)return false;
            CloseBuilder(); return true;
        }
        private void OnBuild(object sender,RoutedEventArgs args) => OpenBuilder(false);
        private void OnEdit(object sender,RoutedEventArgs args) => OpenBuilder(true);
        private void OpenBuilder(bool edit)
        {
            if(Wheel.IsSpinning || saveBlocked) return;
            draft=edit && selected!=null ? selected.Clone() : new RouletteDefinition();
            if(draft.Id==Guid.Empty) {draft.Id=Guid.NewGuid();draft.Name="";}
            if(!edit) {draft.Options.Add(new RouletteOption());draft.Options.Add(new RouletteOption());}
            RouletteName.Text=draft.Name;draftOptions.Clear();foreach(var option in draft.Options)draftOptions.Add(option);
            BuilderStatus.Text="";PlayView.Visibility=Visibility.Collapsed;BuilderView.Visibility=Visibility.Visible;
            RouletteName.Focus(); UpdateAddButton();
        }
        private void OnAddOption(object sender,RoutedEventArgs args)
        {
            if(draftOptions.Count>=32)return;draftOptions.Add(new RouletteOption());UpdateAddButton();
        }
        private void OnRemoveOption(object sender,RoutedEventArgs args)
        {
            var option=(sender as Button)?.Tag as RouletteOption;
            if(option!=null)draftOptions.Remove(option);UpdateAddButton();
        }
        private void UpdateAddButton() { AddOptionButton.IsEnabled=draftOptions.Count<32; }
        private void OnCancel(object sender,RoutedEventArgs args) { CloseBuilder(); }
        private void CloseBuilder() { draft=null;draftOptions.Clear();BuilderView.Visibility=Visibility.Collapsed;PlayView.Visibility=Visibility.Visible; }
        private void OnSave(object sender,RoutedEventArgs args)
        {
            if(draft==null)return;
            draft.Name=RouletteName.Text;draft.Options=draftOptions.Select(o=>o.Clone()).ToList();
            var error=draft.Validate(); if(error!=null){BuilderStatus.Text=error;return;}
            try
            {
                if(store==null) throw new IOException("Saving is unavailable outside the Osiris extension host.");
                store.Save(draft);
                var saved=store.Load();var id=draft.Id;
                roulettes.RemoveAll(r=>r.Id!=Guid.Empty);roulettes.AddRange(saved);
                SavedList.Items.Refresh();CloseBuilder();SavedList.SelectedItem=roulettes.First(r=>r.Id==id);
                PageStatus.Text="Roulette saved.";
            }
            catch(Exception errorSaving) {BuilderStatus.Text="Could not save: "+errorSaving.Message;}
        }
        private void OnUnloaded(object sender,RoutedEventArgs args)
        {
            if(!ReferenceEquals(args.OriginalSource,this))return;
            CloseWinner(false);
            CloseDelete();manageMenu.IsOpen=false;
            Wheel.Clear();CloseBuilder();SetBusy(false);
        }
    }
}
