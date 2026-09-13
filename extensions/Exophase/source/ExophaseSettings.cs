using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Playnite.SDK;
using Playnite.SDK.Data;

namespace Osiris.Extensions.Exophase
{
    public sealed class ExophaseSettings : ObservableObject, ISettings
    {
        private static readonly ILogger Logger = LogManager.GetLogger();
        private ExophasePlugin plugin;
        private ExophaseAuthenticationService authenticationService;
        private ExophaseSettings editingClone;
        private bool connectAccount;
        private bool importAchievements = true;
        private bool importPlayTime = true;
        private bool importPlatforms = true;
        private bool syncOnStartup = true;
        private bool overrideDisplayedPlaytime;
        private string profileReference;
        private string playerProfileId;
        private bool isConnected;
        private bool isConnectionBusy;
        private bool isSynchronizationRunning;
        private string connectionStatus = "Connection has not been checked.";
        private string synchronizationStatus =
            "No Exophase activity has been synchronized yet.";

        public bool ConnectAccount
        {
            get => connectAccount;
            set
            {
                if (connectAccount == value)
                {
                    return;
                }

                SetValue(ref connectAccount, value);
                OnPropertyChanged(nameof(CanAuthenticate));
                if (!value)
                {
                    ConnectionStatus = "Account connection is disabled.";
                }
            }
        }

        public bool ImportAchievements
        {
            get => importAchievements;
            set => SetValue(ref importAchievements, value);
        }

        public bool ImportPlayTime
        {
            get => importPlayTime;
            set => SetValue(ref importPlayTime, value);
        }

        public bool ImportPlatforms
        {
            get => importPlatforms;
            set => SetValue(ref importPlatforms, value);
        }

        public bool SyncOnStartup
        {
            get => syncOnStartup;
            set => SetValue(ref syncOnStartup, value);
        }

        public bool OverrideDisplayedPlaytime
        {
            get => overrideDisplayedPlaytime;
            set => SetValue(ref overrideDisplayedPlaytime, value);
        }

        public string ProfileReference
        {
            get => profileReference;
            set => SetValue(ref profileReference, value?.Trim());
        }

        public string PlayerProfileId
        {
            get => playerProfileId;
            set
            {
                if (playerProfileId == value?.Trim())
                {
                    return;
                }

                SetValue(ref playerProfileId, value?.Trim());
                OnPropertyChanged(nameof(DetectedProfileText));
            }
        }

        [DontSerialize]
        public bool IsConnected
        {
            get => isConnected;
            private set
            {
                if (isConnected == value)
                {
                    return;
                }

                SetValue(ref isConnected, value);
                OnPropertyChanged(nameof(CanSignOut));
                OnPropertyChanged(nameof(AuthenticationButtonText));
            }
        }

        [DontSerialize]
        public bool IsConnectionBusy
        {
            get => isConnectionBusy;
            private set
            {
                if (isConnectionBusy == value)
                {
                    return;
                }

                SetValue(ref isConnectionBusy, value);
                OnPropertyChanged(nameof(CanAuthenticate));
                OnPropertyChanged(nameof(CanSignOut));
            }
        }

        [DontSerialize]
        public string ConnectionStatus
        {
            get => connectionStatus;
            private set => SetValue(ref connectionStatus, value);
        }

        [DontSerialize]
        public bool IsSynchronizationRunning
        {
            get => isSynchronizationRunning;
            private set
            {
                if (isSynchronizationRunning == value)
                {
                    return;
                }

                SetValue(ref isSynchronizationRunning, value);
                OnPropertyChanged(nameof(CanSynchronize));
            }
        }

        [DontSerialize]
        public bool CanSynchronize => !IsSynchronizationRunning;

        [DontSerialize]
        public string SynchronizationStatus
        {
            get => synchronizationStatus;
            private set => SetValue(ref synchronizationStatus, value);
        }

        [DontSerialize]
        public string DetectedProfileText => string.IsNullOrWhiteSpace(PlayerProfileId)
            ? "Profile ID will be detected during the first synchronization."
            : "Detected Exophase profile ID: " + PlayerProfileId;

        [DontSerialize]
        public bool CanAuthenticate => ConnectAccount && !IsConnectionBusy;

        [DontSerialize]
        public bool CanSignOut => IsConnected && !IsConnectionBusy;

        [DontSerialize]
        public string AuthenticationButtonText => IsConnected ? "Reconnect" : "Sign in to Exophase";

        internal void Attach(ExophasePlugin owner, ExophaseAuthenticationService service)
        {
            plugin = owner ?? throw new ArgumentNullException(nameof(owner));
            authenticationService = service ?? throw new ArgumentNullException(nameof(service));
        }

        internal async Task SynchronizeAsync(bool showFooter = true)
        {
            if (plugin == null || IsSynchronizationRunning)
            {
                return;
            }

            IsSynchronizationRunning = true;
            SynchronizationStatus = "Preparing Exophase activity synchronization...";
            var footerProgress = showFooter
                ? OsirisFooterUpdateProgress.TryStart(
                    "Synchronizing Exophase activity",
                    SynchronizationStatus)
                : null;
            var cancellationToken = footerProgress?.CancellationToken ??
                                    System.Threading.CancellationToken.None;
            try
            {
                var progress = new Progress<string>(message =>
                {
                    SynchronizationStatus = message;
                    footerProgress?.Report("Synchronizing Exophase activity", message);
                });
                var summary = await plugin.SynchronizeAsync(
                    this,
                    progress,
                    cancellationToken);
                OnPropertyChanged(nameof(DetectedProfileText));
                var importedHours = Math.Round(
                    summary.ImportedPlaytimeSeconds / 3600d,
                    1,
                    MidpointRounding.AwayFromZero);
                SynchronizationStatus =
                    $"Read {summary.Records} platform records for {summary.Games} games. " +
                    $"Matched {summary.MatchedGames}; skipped {summary.UnmatchedGames} unmatched " +
                    $"and {summary.AmbiguousGames} ambiguous. " +
                    $"Matched Exophase time: {importedHours.ToString("0.#", CultureInfo.InvariantCulture)} hours.";
                Logger.Info(SynchronizationStatus);
                footerProgress?.Complete("Exophase activity synchronization completed");
            }
            catch (OperationCanceledException)
            {
                SynchronizationStatus = "Exophase synchronization cancelled.";
                footerProgress?.Cancel();
            }
            catch (Exception exception)
            {
                SynchronizationStatus = "Exophase synchronization stopped: " + exception.Message;
                Logger.Warn(exception, "Exophase activity synchronization failed.");
                footerProgress?.Complete("Exophase activity synchronization failed");
            }
            finally
            {
                IsSynchronizationRunning = false;
            }
        }

        internal async Task RefreshConnectionStatusAsync()
        {
            if (!ConnectAccount)
            {
                IsConnected = false;
                ConnectionStatus = "Account connection is disabled.";
                return;
            }

            if (authenticationService == null || IsConnectionBusy)
            {
                return;
            }

            IsConnectionBusy = true;
            ConnectionStatus = "Checking Exophase sign-in...";
            try
            {
                IsConnected = await Task.Run(
                    () => authenticationService.HasAuthenticatedSession());
                ConnectionStatus = IsConnected
                    ? "Connected to Exophase in Osiris."
                    : "Not signed in to Exophase.";
            }
            catch
            {
                IsConnected = false;
                ConnectionStatus = "Osiris could not verify the Exophase session.";
            }
            finally
            {
                IsConnectionBusy = false;
            }
        }

        internal async Task AuthenticateAsync()
        {
            if (!CanAuthenticate || authenticationService == null)
            {
                return;
            }

            IsConnectionBusy = true;
            ConnectionStatus = "Waiting for Exophase sign-in...";
            try
            {
                IsConnected = authenticationService.Login();
                ConnectionStatus = IsConnected
                    ? "Connected to Exophase in Osiris."
                    : "Exophase sign-in was not completed.";
            }
            catch
            {
                IsConnected = false;
                ConnectionStatus = "Exophase sign-in failed.";
            }
            finally
            {
                IsConnectionBusy = false;
            }

            if (IsConnected)
            {
                await SynchronizeAsync();
            }
        }

        internal void SignOut()
        {
            if (!CanSignOut || authenticationService == null)
            {
                return;
            }

            IsConnectionBusy = true;
            try
            {
                authenticationService.ClearSession();
                IsConnected = false;
                ConnectionStatus = "Signed out of Exophase in Osiris.";
            }
            catch
            {
                ConnectionStatus = "Osiris could not clear the Exophase session.";
            }
            finally
            {
                IsConnectionBusy = false;
            }
        }

        public void BeginEdit()
        {
            editingClone = Clone();
        }

        public void CancelEdit()
        {
            if (editingClone != null)
            {
                CopyFrom(editingClone);
            }
        }

        public void EndEdit()
        {
            plugin?.SaveSettings(this);
            editingClone = null;
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            if (!string.IsNullOrWhiteSpace(ProfileReference))
            {
                try
                {
                    string numericId;
                    if (!ExophaseActivityParser.TryParseNumericProfileId(ProfileReference, out numericId))
                    {
                        ExophaseActivityParser.GetProfilePageUrl(ProfileReference);
                    }
                }
                catch (ArgumentException exception)
                {
                    errors.Add(exception.Message);
                }
            }

            return errors.Count == 0;
        }

        internal ExophaseSettings Clone()
        {
            return new ExophaseSettings
            {
                ConnectAccount = ConnectAccount,
                ImportAchievements = ImportAchievements,
                ImportPlayTime = ImportPlayTime,
                ImportPlatforms = ImportPlatforms,
                SyncOnStartup = SyncOnStartup,
                OverrideDisplayedPlaytime = OverrideDisplayedPlaytime,
                ProfileReference = ProfileReference,
                PlayerProfileId = PlayerProfileId
            };
        }

        private void CopyFrom(ExophaseSettings source)
        {
            ConnectAccount = source?.ConnectAccount ?? false;
            ImportAchievements = source?.ImportAchievements ?? true;
            ImportPlayTime = source?.ImportPlayTime ?? true;
            ImportPlatforms = source?.ImportPlatforms ?? true;
            SyncOnStartup = source?.SyncOnStartup ?? true;
            OverrideDisplayedPlaytime = source?.OverrideDisplayedPlaytime ?? false;
            ProfileReference = source?.ProfileReference;
            PlayerProfileId = source?.PlayerProfileId;
            OnPropertyChanged(nameof(DetectedProfileText));
        }
    }
}
