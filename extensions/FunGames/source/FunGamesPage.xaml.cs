using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.IO;
using System.Collections.Generic;
using System.Windows.Media.Imaging;
using Playnite.SDK;

namespace Osiris.Extensions.FunGames
{
    public sealed partial class FunGamesPage : UserControl
    {
        private readonly FrameworkElement header;
        private readonly IPlayniteAPI api;
        private readonly string dataFolder;

        public FunGamesPage() : this(null,null) { }

        internal FunGamesPage(IPlayniteAPI api,string dataFolder)
        {
            this.api = api;
            this.dataFolder=dataFolder;
            InitializeComponent();
            header = CreateHeader();
            FixedHeaderHost.Content = header;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private static FrameworkElement CreateHeader()
        {
            // Use the same native, themed top panel as the Exophase/HLTB pages.
            try
            {
                var type = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("Playnite.DesktopApp.Controls.Views.TopPanel", false))
                    .FirstOrDefault(t => t != null);
                if (type != null) return (FrameworkElement)Activator.CreateInstance(type);
            }
            catch { /* A standalone validation host has no DesktopApp panel. */ }
            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(9,9,9)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(28,28,28)),
                BorderThickness = new Thickness(0,0,0,1),
                Child = new TextBlock { Text = "Fun Games", Margin = new Thickness(40,0,0,0), VerticalAlignment = VerticalAlignment.Center }
            };
        }

        private void OnLoaded(object sender, RoutedEventArgs args)
        {
            if (!ReferenceEquals(args.OriginalSource, this)) return;
            var window = Application.Current?.MainWindow;
            if (window != null)
                header.SetBinding(DataContextProperty, new Binding("DataContext") { Source = window, Mode = BindingMode.OneWay });
            LoadHeroArtwork();
        }

        private void OnUnloaded(object sender, RoutedEventArgs args)
        {
            if (!ReferenceEquals(args.OriginalSource, this)) return;
            BindingOperations.ClearBinding(header, DataContextProperty);
            WheelArtwork.SetHeroes(null);
            CloseRoulette();
        }

        private void OnPlayRoulette(object sender,RoutedEventArgs args)
        {
            args.Handled=true;
            if(RouletteHost.Content!=null)return;
            var page=new RoulettePage(api,dataFolder);
            RouletteHost.Content=page;GamesCatalog.Visibility=Visibility.Collapsed;RouletteHost.Visibility=Visibility.Visible;
        }
        private void CloseRoulette()
        {
            RouletteHost.Content=null;RouletteHost.Visibility=Visibility.Collapsed;GamesCatalog.Visibility=Visibility.Visible;
        }

        // Called by Osiris's existing search-panel Back action before page history.
        public bool OsirisTryNavigateBack()
        {
            var page=RouletteHost.Content as RoulettePage;
            if(page==null)return false;
            if(page.TryNavigateBack())return true;
            CloseRoulette();return true;
        }

        private void LoadHeroArtwork()
        {
            var heroes = new List<ImageSource>();
            if (api != null)
            {
                try
                {
                    // Decorative read-only sampling, not a game selection or launch.
                    var paths = api.Database.Games.Where(g => !g.Hidden && !string.IsNullOrWhiteSpace(g.BackgroundImage))
                        .Select(g => g.BackgroundImage).Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(p => Guid.NewGuid()).Take(24);
                    foreach (var databasePath in paths)
                    {
                        try
                        {
                            var path = api.Database.GetFullFilePath(databasePath);
                            if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
                            using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                            {
                                var image = new BitmapImage();
                                image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
                                image.DecodePixelWidth = 600; image.StreamSource = stream; image.EndInit();
                                image.Freeze(); heroes.Add(image);
                            }
                            if (heroes.Count == 8) break;
                        }
                        catch { /* Missing or unsupported artwork should not prevent opening the hub. */ }
                    }
                }
                catch { /* Empty/unavailable libraries keep the decorative gradient wheel. */ }
            }
            WheelArtwork.SetHeroes(heroes);
        }
    }
}
