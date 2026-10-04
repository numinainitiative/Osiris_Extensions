using System.Collections.Generic;
using Playnite.SDK;

namespace Osiris.Extensions.ScreenshotsGallery
{
    public sealed class ScreenshotsSettings : ObservableObject
    {
        private string expandedExperience = "FullScreen";
        public string ExpandedExperience
        {
            get => expandedExperience;
            set => SetValue(ref expandedExperience, value == "Cinematic" ? "Cinematic" : "FullScreen");
        }
    }

    public sealed class ScreenshotsSettingsViewModel : ObservableObject, ISettings
    {
        private readonly ScreenshotsPlugin plugin;
        private ScreenshotsSettings settings;
        private string editingMode;
        public ScreenshotsSettings Settings
        {
            get => settings;
            set { settings = value; OnPropertyChanged(); }
        }
        public ScreenshotsSettingsViewModel(ScreenshotsPlugin plugin)
        {
            this.plugin = plugin;
            Settings = plugin.LoadPluginSettings<ScreenshotsSettings>() ?? new ScreenshotsSettings();
        }
        public void BeginEdit() { editingMode = Settings.ExpandedExperience; }
        public void CancelEdit() { Settings.ExpandedExperience = editingMode; }
        public void EndEdit() { plugin.SavePluginSettings(Settings); }
        public bool VerifySettings(out List<string> errors) { errors = new List<string>(); return true; }
    }
}
