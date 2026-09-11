using System.Windows;
using System.Windows.Controls;

namespace Osiris.Extensions.HowLongToBeat
{
    public partial class HowLongToBeatSettingsView : UserControl
    {
        public HowLongToBeatSettingsView()
        {
            InitializeComponent();
        }

        private async void OnUpdateDatabaseClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            var settings = DataContext as HowLongToBeatSettings;
            if (settings != null)
            {
                await settings.UpdateDatabaseAsync();
            }
        }
    }
}
