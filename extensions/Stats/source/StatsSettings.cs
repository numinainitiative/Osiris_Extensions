using System;
using System.Collections.Generic;
using Playnite.SDK;

namespace Osiris.Extensions.Stats
{
    public sealed class StatsSettings : ObservableObject
    {
        private bool globalEnabled = true, globalLongestSession = true, perGameEnabled = true, perGameLongestSession = true, perGameAge = true;
        private bool globalMostPlayed = true, globalAnniversaries = true, globalOldestGame = true, globalTotalPlaytime = true, globalUnplayedReminder = true, globalFirstPlayedBirthday = true;
        public bool GlobalEnabled { get => globalEnabled; set => SetValue(ref globalEnabled, value); }
        public bool GlobalLongestSession { get => globalLongestSession; set => SetValue(ref globalLongestSession, value); }
        public bool GlobalMostPlayed { get => globalMostPlayed; set => SetValue(ref globalMostPlayed, value); }
        public bool GlobalAnniversaries { get => globalAnniversaries; set => SetValue(ref globalAnniversaries, value); }
        public bool GlobalOldestGame { get => globalOldestGame; set => SetValue(ref globalOldestGame, value); }
        public bool GlobalTotalPlaytime { get => globalTotalPlaytime; set => SetValue(ref globalTotalPlaytime, value); }
        public bool GlobalUnplayedReminder { get => globalUnplayedReminder; set => SetValue(ref globalUnplayedReminder, value); }
        public bool GlobalFirstPlayedBirthday { get => globalFirstPlayedBirthday; set => SetValue(ref globalFirstPlayedBirthday, value); }
        public bool PerGameEnabled { get => perGameEnabled; set => SetValue(ref perGameEnabled, value); }
        public bool PerGameLongestSession { get => perGameLongestSession; set => SetValue(ref perGameLongestSession, value); }
        public bool PerGameAge { get => perGameAge; set => SetValue(ref perGameAge, value); }
        internal bool AnyGlobalEnabled => GlobalLongestSession || GlobalMostPlayed || GlobalAnniversaries || GlobalOldestGame ||
            GlobalTotalPlaytime || GlobalUnplayedReminder || GlobalFirstPlayedBirthday;
        internal StatsSettings Clone() => new StatsSettings { GlobalEnabled = GlobalEnabled, GlobalLongestSession = GlobalLongestSession,
            GlobalMostPlayed = GlobalMostPlayed, GlobalAnniversaries = GlobalAnniversaries, GlobalOldestGame = GlobalOldestGame,
            GlobalTotalPlaytime = GlobalTotalPlaytime, GlobalUnplayedReminder = GlobalUnplayedReminder, GlobalFirstPlayedBirthday = GlobalFirstPlayedBirthday,
            PerGameEnabled = PerGameEnabled, PerGameLongestSession = PerGameLongestSession, PerGameAge = PerGameAge };
    }

    public sealed class StatsSettingsViewModel : ObservableObject, ISettings
    {
        private readonly StatsPlugin plugin;
        private StatsSettings settings;
        internal StatsSettings Committed { get; private set; }
        public StatsSettings Settings { get => settings; private set { settings = value; OnPropertyChanged(); } }
        internal StatsSettingsViewModel(StatsPlugin plugin)
        {
            this.plugin = plugin;
            Committed = plugin.LoadPluginSettings<StatsSettings>() ?? new StatsSettings();
            Settings = Committed.Clone();
        }
        public void BeginEdit() { Settings = Committed.Clone(); }
        public void CancelEdit() { Settings = Committed.Clone(); }
        public void EndEdit()
        {
            var saved = Settings.Clone();
            plugin.SavePluginSettings(saved);
            Committed = saved;
            plugin.InsightsSettingsChanged();
        }
        public bool VerifySettings(out List<string> errors) { errors = new List<string>(); return true; }
    }
}
