using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Shell;
namespace Osiris.Extensions.Trophies
{
    public partial class TrophiesWindow : Window
    {
        private readonly List<Trophy> entries;
        internal TrophiesWindow(string game, IEnumerable<Trophy> trophies)
        {
            entries = trophies.ToList();
            InitializeComponent(); Heading.Text = game + " Trophies  " + entries.Count(t => t.UnlockedUtc.HasValue) + "/" + entries.Count;
            AllTrophies.ItemsSource = TrophyRow.Group(entries, true);
            AllTrophies.GroupStyle.Add(new System.Windows.Controls.GroupStyle { HeaderTemplate = (DataTemplate)FindResource("AchievementCategory") });
            Width = Math.Min(1225, Math.Max(760, SystemParameters.WorkArea.Width - 80));
            Height = Math.Min(900, Math.Max(480, SystemParameters.WorkArea.Height - 80));
            WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 0, CornerRadius = new CornerRadius(8), GlassFrameThickness = new Thickness(0), ResizeBorderThickness = new Thickness(6), UseAeroCaptionButtons = false });
            var presenter = new FrameworkElementFactory(typeof(System.Windows.Controls.ContentPresenter));
            presenter.SetValue(System.Windows.Controls.ContentPresenter.ContentSourceProperty, "Content");
            var decorator = new FrameworkElementFactory(typeof(System.Windows.Documents.AdornerDecorator)); decorator.AppendChild(presenter);
            Template = new System.Windows.Controls.ControlTemplate(typeof(Window)) { VisualTree = decorator };
        }
        private void SortChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (AllTrophies != null && entries != null) AllTrophies.ItemsSource = TrophyRow.Group(entries, true, rarity: SortBy.SelectedIndex == 1, lastAchieved: SortBy.SelectedIndex == 2);
        }
        private void MinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void MaximizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        private void CloseClick(object sender, RoutedEventArgs e) => Close();
    }
}
