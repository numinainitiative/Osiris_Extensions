using System.Windows;
using System.Windows.Controls;
namespace Osiris.Extensions.Trophies
{
    public partial class TrophiesSettingsView : UserControl
    {
        private readonly TrophiesSettingsModel model;
        internal TrophiesSettingsView(TrophiesSettingsModel model)
        {
            this.model = model; InitializeComponent(); DataContext = model;
            Loaded += (s, e) => ApiKeyBox.Password = model.PendingKey ?? "";
        }
        private void KeyChanged(object sender, RoutedEventArgs e) { if (model != null) model.PendingKey = ApiKeyBox.Password; }
    }
}
