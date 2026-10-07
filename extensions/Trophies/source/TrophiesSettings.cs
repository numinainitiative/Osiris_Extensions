using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Playnite.SDK;

namespace Osiris.Extensions.Trophies
{
    public sealed class TrophiesSettings
    {
        public bool Enabled { get; set; } = true;
        public string ProtectedApiKey { get; set; }
    }
    public sealed class TrophiesSettingsModel : ObservableObject, ISettings
    {
        private readonly TrophiesPlugin plugin;
        private bool enabled;
        public bool Enabled { get => enabled; set => SetValue(ref enabled, value); }
        private string exophaseStatus="Uses editions already linked in Exophase. Press Sync in Game Edit → Trophies to update unlocked trophies.";
        public string ExophaseStatus { get => exophaseStatus; internal set => SetValue(ref exophaseStatus,value); }
        internal string PendingKey { get; set; }
        internal TrophiesSettings Committed { get; private set; }
        internal TrophiesSettingsModel(TrophiesPlugin plugin)
        { this.plugin = plugin; Committed = plugin.LoadPluginSettings<TrophiesSettings>() ?? new TrophiesSettings(); BeginEdit(); }
        internal string ApiKey
        {
            get { try { return string.IsNullOrEmpty(Committed.ProtectedApiKey) ? "" : Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(Committed.ProtectedApiKey), null, DataProtectionScope.CurrentUser)); } catch { return ""; } }
        }
        public void BeginEdit() { Enabled = Committed.Enabled; PendingKey = ApiKey; }
        public void CancelEdit() { BeginEdit(); }
        public void EndEdit()
        {
            var key = (PendingKey ?? "").Trim();
            var saved = new TrophiesSettings { Enabled = Enabled, ProtectedApiKey = key.Length == 0 ? null : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(key), null, DataProtectionScope.CurrentUser)) };
            plugin.SavePluginSettings(saved); Committed = saved;
        }
        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            var key = (PendingKey ?? "").Trim();
            if (key.Length != 0 && !System.Text.RegularExpressions.Regex.IsMatch(key, "\\A[0-9a-fA-F]{32}\\z")) errors.Add("Enter a valid 32-character Steam Web API key, or leave it empty.");
            return errors.Count == 0;
        }
    }
}
