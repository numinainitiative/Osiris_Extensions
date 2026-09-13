using System.Windows;
using System.Windows.Controls;

namespace Osiris.Extensions.Exophase
{
    public partial class ExophaseSettingsView : UserControl
    {
        private bool connectionChecked;

        public ExophaseSettingsView()
        {
            InitializeComponent();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (connectionChecked)
            {
                return;
            }

            connectionChecked = true;
            var settings = DataContext as ExophaseSettings;
            if (settings != null)
            {
                await settings.RefreshConnectionStatusAsync();
            }
        }

        private async void OnConnectAccountChanged(object sender, RoutedEventArgs e)
        {
            var settings = DataContext as ExophaseSettings;
            if (settings?.ConnectAccount == true)
            {
                await settings.RefreshConnectionStatusAsync();
            }
        }

        private async void OnAuthenticateClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            var settings = DataContext as ExophaseSettings;
            if (settings != null)
            {
                await settings.AuthenticateAsync();
            }
        }

        private void OnSignOutClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            (DataContext as ExophaseSettings)?.SignOut();
        }

        private async void OnSynchronizeClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            var settings = DataContext as ExophaseSettings;
            if (settings != null)
            {
                await settings.SynchronizeAsync();
            }
        }
    }
}
