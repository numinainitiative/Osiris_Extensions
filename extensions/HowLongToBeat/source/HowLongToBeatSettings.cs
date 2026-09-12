using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Playnite.SDK;
using Playnite.SDK.Data;

namespace Osiris.Extensions.HowLongToBeat
{
    public static class CompletionTimeProfiles
    {
        public const string Rushed = "Rushed";
        public const string Average = "Average";
        public const string Median = "Median";
        public const string Leisure = "Leisure";

        public static bool IsValid(string value)
        {
            return string.Equals(value, Rushed, StringComparison.Ordinal) ||
                   string.Equals(value, Average, StringComparison.Ordinal) ||
                   string.Equals(value, Median, StringComparison.Ordinal) ||
                   string.Equals(value, Leisure, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Global completion-time display preferences. Playnite calls the ISettings
    /// transaction methods when Osiris's shared extension settings host opens,
    /// saves, or cancels the page.
    /// </summary>
    public sealed class HowLongToBeatSettings : ObservableObject, ISettings
    {
        private HowLongToBeatPlugin plugin;
        private HowLongToBeatSettings editingClone;
        private string timeProfile = CompletionTimeProfiles.Average;
        private bool showMainStory = true;
        private bool showMainExtras = true;
        private bool showCompletionist = true;
        private bool isDatabaseUpdateRunning;
        private string databaseUpdateStatus =
            "Only games with an existing HowLongToBeat match will be checked.";

        public event Action SettingsChanged;

        public string TimeProfile
        {
            get => timeProfile;
            set => SetValue(ref timeProfile, CompletionTimeProfiles.IsValid(value)
                ? value
                : CompletionTimeProfiles.Average);
        }

        public bool ShowMainStory
        {
            get => showMainStory;
            set => SetValue(ref showMainStory, value);
        }

        public bool ShowMainExtras
        {
            get => showMainExtras;
            set => SetValue(ref showMainExtras, value);
        }

        public bool ShowCompletionist
        {
            get => showCompletionist;
            set => SetValue(ref showCompletionist, value);
        }

        [DontSerialize]
        public bool IsDatabaseUpdateRunning
        {
            get => isDatabaseUpdateRunning;
            private set
            {
                if (isDatabaseUpdateRunning == value)
                {
                    return;
                }

                SetValue(ref isDatabaseUpdateRunning, value);
                OnPropertyChanged(nameof(CanUpdateDatabase));
            }
        }

        [DontSerialize]
        public bool CanUpdateDatabase => !IsDatabaseUpdateRunning;

        [DontSerialize]
        public string DatabaseUpdateStatus
        {
            get => databaseUpdateStatus;
            private set => SetValue(ref databaseUpdateStatus, value);
        }

        public bool HasVisibleTimes => ShowMainStory || ShowMainExtras || ShowCompletionist;

        internal void Attach(HowLongToBeatPlugin owner)
        {
            plugin = owner ?? throw new ArgumentNullException(nameof(owner));
            Normalize();
        }

        internal async Task UpdateDatabaseAsync()
        {
            if (IsDatabaseUpdateRunning)
            {
                return;
            }

            if (plugin == null)
            {
                DatabaseUpdateStatus = "The HowLongToBeat database updater is unavailable.";
                return;
            }

            IsDatabaseUpdateRunning = true;
            DatabaseUpdateStatus = "Preparing stored HowLongToBeat matches...";
            var footerProgress = OsirisFooterUpdateProgress.TryStart(
                "Updating How Long To Beat database",
                DatabaseUpdateStatus);
            var cancellationToken = footerProgress?.CancellationToken ?? CancellationToken.None;
            try
            {
                var progress = new Progress<string>(message =>
                {
                    DatabaseUpdateStatus = message;
                    footerProgress?.Report(
                        "Updating How Long To Beat database",
                        message);
                });
                var summary = await plugin.UpdateStoredDatabaseAsync(
                    progress,
                    cancellationToken);
                DatabaseUpdateStatus = summary.CheckedGames == 0
                    ? $"No fetched matches were found. {summary.LibraryGames} library games were left untouched."
                    : $"Checked {summary.CheckedGames} fetched games: " +
                      $"{summary.UpdatedGames} updated, {summary.UnchangedGames} unchanged, " +
                      $"{summary.FailedGames} failed. {summary.SkippedGames} unfetched games were untouched.";
                footerProgress?.Complete("All tasks are now completed");
            }
            catch (OperationCanceledException)
            {
                DatabaseUpdateStatus = "Database update cancelled.";
                footerProgress?.Cancel();
            }
            catch (Exception exception)
            {
                DatabaseUpdateStatus = "Database update stopped: " + exception.Message;
                footerProgress?.Complete("How Long To Beat database update failed");
            }
            finally
            {
                IsDatabaseUpdateRunning = false;
            }
        }

        public void BeginEdit()
        {
            editingClone = Clone();
        }

        public void CancelEdit()
        {
            if (editingClone == null)
            {
                return;
            }

            CopyFrom(editingClone);
            SettingsChanged?.Invoke();
        }

        public void EndEdit()
        {
            Normalize();
            plugin?.SavePluginSettings(this);
            editingClone = null;
            SettingsChanged?.Invoke();
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            if (!CompletionTimeProfiles.IsValid(TimeProfile))
            {
                errors.Add("Choose a valid completion-time profile.");
            }

            return errors.Count == 0;
        }

        internal HowLongToBeatSettings Clone()
        {
            return new HowLongToBeatSettings
            {
                TimeProfile = TimeProfile,
                ShowMainStory = ShowMainStory,
                ShowMainExtras = ShowMainExtras,
                ShowCompletionist = ShowCompletionist
            };
        }

        private void CopyFrom(HowLongToBeatSettings source)
        {
            TimeProfile = source?.TimeProfile;
            ShowMainStory = source?.ShowMainStory ?? true;
            ShowMainExtras = source?.ShowMainExtras ?? true;
            ShowCompletionist = source?.ShowCompletionist ?? true;
            Normalize();
        }

        private void Normalize()
        {
            if (!CompletionTimeProfiles.IsValid(TimeProfile))
            {
                TimeProfile = CompletionTimeProfiles.Average;
            }
        }
    }
}
