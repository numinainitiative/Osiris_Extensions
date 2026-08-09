using System;
using System.Collections.Generic;
using System.Diagnostics;
using Playnite.SDK;

namespace Osiris.Extensions.SteamGridDBMetadata
{
    public sealed class SteamGridDbSettingsViewModel : ObservableObject, ISettings
    {
        private readonly SteamGridDbMetadataPlugin plugin;
        private SteamGridDbSettings editingClone;
        private SteamGridDbSettings settings;

        public SteamGridDbSettings Settings
        {
            get => settings;
            private set
            {
                settings = value;
                OnPropertyChanged();
            }
        }

        public RelayCommand LoginCommand { get; }

        public SteamGridDbSettingsViewModel(SteamGridDbMetadataPlugin plugin)
        {
            this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            Settings = plugin.LoadPluginSettings<SteamGridDbSettings>() ?? new SteamGridDbSettings();
            EnsureDefaults(Settings);
            LoginCommand = new RelayCommand(OpenApiKeyPage);
        }

        public void BeginEdit()
        {
            editingClone = Settings.Clone();
        }

        public void CancelEdit()
        {
            if (editingClone != null)
            {
                Settings = editingClone.Clone();
            }
        }

        public void EndEdit()
        {
            EnsureDefaults(Settings);
            plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            if (!string.Equals(Settings.SimilarMediaApplication, "Different", StringComparison.Ordinal) &&
                !string.Equals(Settings.SimilarMediaApplication, "Same", StringComparison.Ordinal))
            {
                errors.Add("Choose either Different media or Same media.");
            }

            return errors.Count == 0;
        }

        private static void EnsureDefaults(SteamGridDbSettings value)
        {
            if (string.IsNullOrWhiteSpace(value.SimilarMediaApplication))
            {
                value.SimilarMediaApplication = "Different";
            }

            value.ApiKey = value.ApiKey ?? string.Empty;
        }

        private static void OpenApiKeyPage()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.steamgriddb.com/profile/preferences/api",
                UseShellExecute = true
            });
        }
    }
}
