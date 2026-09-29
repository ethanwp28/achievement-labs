using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using AchievementLabs.Models;


namespace AchievementLabs.Desktop.Workflows
{
    public partial class AutoUnlockerViewModel : ObservableObject, INativePageLifecycle
    {
        private readonly NativeAccountContext account;
        public AchievementLabs.Core.EventCatalogClient? EventCatalog { get; set; }
        private readonly NativeNotices _snackbarService;
        private readonly TimeSpan _snackbarDuration = TimeSpan.FromSeconds(2);
        private XboxApiClient? _xboxRestAPI;
        private string _lastKnownXauth = "";

        private CancellationTokenSource? _cancellationTokenSource;
        private AutoUnlockState? _state;

        /// <summary>
        /// Gets the current XboxApiClient instance, recreating it if the XAUTH token has changed.
        /// This ensures the auto unlocker keeps working when the token refreshes.
        /// </summary>
        private XboxApiClient GetRestAPI()
        {
            if (_xboxRestAPI == null || account.XAUTH != _lastKnownXauth)
            {
                _xboxRestAPI?.Dispose();
                _lastKnownXauth = account.XAUTH;
                _xboxRestAPI = new XboxApiClient(account.XAUTH);
            }
            return _xboxRestAPI;
        }

        public void ReleaseClient()
        {
            if (IsRunning) return;
            _xboxRestAPI?.Dispose();
            _xboxRestAPI = null;
            _lastKnownXauth = "";
        }

        public AutoUnlockerViewModel(NativeNotices snackbarService, NativeAccountContext account)
        {
            this.account = account;
            _snackbarService = snackbarService;
        }

        #region Observable Properties

        [ObservableProperty] private bool _isInitialized = false;
        [ObservableProperty] private string _referenceGamertag = "";
        [ObservableProperty] private string _titleId = "";
        [ObservableProperty] private string _statusText = "Idle";
        [ObservableProperty] private string _startStopButtonText = "Start Auto Unlock";
        [ObservableProperty] private bool _isRunning = false;
        [ObservableProperty] private bool _isConfigEnabled = true;
        [ObservableProperty] private string _gameName = "Game: None";
        [ObservableProperty] private string _referenceInfo = "Reference User: None";
        [ObservableProperty] private string _progressText = "Progress: 0/0";
        [ObservableProperty] private string _nextUnlockText = "Next Unlock: N/A";
        [ObservableProperty] private string _timeRemainingText = "Time Until Next: N/A";
        [ObservableProperty] private double _speedMultiplier = 1.0;
        [ObservableProperty] private string _speedDisplay = "Speed: 1.0x (Real Time)";
        [ObservableProperty] private ObservableCollection<AutoUnlockQueueDisplay> _queueItems = new();
        [ObservableProperty] private bool _hasExistingState = false;
        [ObservableProperty] private string _existingStateInfo = "";

        #endregion

        public class AutoUnlockQueueDisplay
        {
            private decimal _delayMinutes;

            public int Index { get; set; }
            public string AchievementName { get; set; } = "";
            public string AchievementId { get; set; } = "";
            public int Gamerscore { get; set; }
            public bool CanEditDelay { get; set; }
            internal Action<int, decimal>? DelayChanged { get; set; }
            public decimal DelayMinutes
            {
                get => _delayMinutes;
                set
                {
                    var normalized = Math.Max(0, value);
                    if (_delayMinutes == normalized) return;
                    _delayMinutes = normalized;
                    DelayChanged?.Invoke(Index - 1, normalized);
                }
            }
            public string Status { get; set; } = "Pending";
        }

        public void OnNavigatedTo()
        {
            if (!IsInitialized && account.InitComplete)
                InitializeViewModel();
        }

        public void OnNavigatedFrom()
        {
        }

        private void InitializeViewModel()
        {
            IsInitialized = true;
            CheckForExistingState();
        }

        private void CheckForExistingState()
        {
            var state = AutoUnlockState.Load();
            if (state != null && state.Queue.Count > 0 && state.CurrentIndex < state.Queue.Count)
            {
                HasExistingState = true;
                var completed = state.Queue.Count(q => q.Completed);
                ExistingStateInfo = $"Saved session: {state.GameName} ({state.ReferenceGamertag}) - {completed}/{state.Queue.Count} completed";
            }
            else
            {
                HasExistingState = false;
                ExistingStateInfo = "";
            }
        }

        [RelayCommand]
        public async Task ResumeExistingSession()
        {
            var state = AutoUnlockState.Load();
            if (state == null)
            {
                _snackbarService.Show("Error", "No saved session found.", NoticeAppearance.Danger,
                    new NoticeIcon(NoticeSymbol.ErrorCircle24), _snackbarDuration);
                return;
            }

            _state = state;
            ReferenceGamertag = state.ReferenceGamertag;
            TitleId = state.TitleId;
            GameName = $"Game: {state.GameName}";
            ReferenceInfo = $"Reference User: {state.ReferenceGamertag} ({state.ReferenceXuid})";
            SpeedMultiplier = state.SpeedMultiplier;
            UpdateSpeedDisplay();
            PopulateQueueDisplay();
            UpdateProgressText();

            _snackbarService.Show("Session Restored", $"Resuming auto unlock for {state.GameName}",
                NoticeAppearance.Success, new NoticeIcon(NoticeSymbol.Checkmark24), _snackbarDuration);

            await StartAutoUnlockProcess();
        }

        [RelayCommand]
        public Task ClearExistingSession()
        {
            AutoUnlockState.Delete();
            HasExistingState = false;
            ExistingStateInfo = "";
            QueueItems.Clear();
            _state = null;
            StatusText = "Idle";
            GameName = "Game: None";
            ReferenceInfo = "Reference User: None";
            ProgressText = "Progress: 0/0";
            NextUnlockText = "Next Unlock: N/A";
            TimeRemainingText = "Time Until Next: N/A";

            _snackbarService.Show("Session Cleared", "Saved auto unlock session has been deleted.",
                NoticeAppearance.Success, new NoticeIcon(NoticeSymbol.Checkmark24), _snackbarDuration);
            return Task.CompletedTask;
        }

        [RelayCommand]
        public async Task BuildQueue()
        {
            if (string.IsNullOrWhiteSpace(ReferenceGamertag) || string.IsNullOrWhiteSpace(TitleId))
            {
                _snackbarService.Show("Error", "Please enter both a reference gamertag and a title ID.",
                    NoticeAppearance.Danger, new NoticeIcon(NoticeSymbol.ErrorCircle24), _snackbarDuration);
                return;
            }

            StatusText = "Fetching reference user profile...";
            IsConfigEnabled = false;

            try
            {
                // Step 1: Resolve reference gamertag to XUID
                var profileData = await GetRestAPI().GetGamertagProfileAsync(ReferenceGamertag);
                if (profileData == null)
                {
                    _snackbarService.Show("Error", "Failed to fetch gamertag profile.",
                        NoticeAppearance.Danger, new NoticeIcon(NoticeSymbol.ErrorCircle24), _snackbarDuration);
                    StatusText = "Failed to resolve gamertag.";
                    IsConfigEnabled = true;
                    return;
                }

                var profileUser = profileData["profileUsers"]?.FirstOrDefault();
                if (profileUser == null)
                {
                    _snackbarService.Show("Error", "No profile found for that gamertag.",
                        NoticeAppearance.Danger, new NoticeIcon(NoticeSymbol.ErrorCircle24), _snackbarDuration);
                    StatusText = "Gamertag not found.";
                    IsConfigEnabled = true;
                    return;
                }

                var refXuid = profileUser["id"]?.ToString() ?? "";
                var refGamertagResolved = profileUser["settings"]?.FirstOrDefault(s => s["id"]?.ToString() == "Gamertag")?["value"]?.ToString() ?? ReferenceGamertag;

                ReferenceInfo = $"Reference User: {refGamertagResolved} ({refXuid})";
                StatusText = "Fetching reference user achievements...";

                // Step 2: Fetch reference user's achievements for this title
                var refAchievements = await GetRestAPI().GetAchievementsForTitleAsync(refXuid, TitleId);
                if (refAchievements == null || refAchievements.achievements.Count == 0)
                {
                    _snackbarService.Show("Error", "No achievements found for this title on the reference user.",
                        NoticeAppearance.Danger, new NoticeIcon(NoticeSymbol.ErrorCircle24), _snackbarDuration);
                    StatusText = "No achievements found.";
                    IsConfigEnabled = true;
                    return;
                }

                // Get service config ID from achievements
                var serviceConfigId = refAchievements.achievements[0].serviceConfigId;

                // Detect event-based game: if the first achievement's requirement ID is not the zero GUID, it's event-based
                bool isEventBased = false;
                var firstAch = refAchievements.achievements[0];
                if (firstAch.progression?.requirements?.Count > 0 &&
                    firstAch.progression.requirements[0].id != Guid.Empty.ToString())
                {
                    isEventBased = true;
                }

                HashSet<string> licensedIds = [];
                if (isEventBased)
                {
                    if (EventCatalog == null) throw new InvalidOperationException("The event template service is unavailable.");
                    var catalog = await EventCatalog.GetCatalogAsync(CancellationToken.None);
                    if (!catalog.TitleIds.Contains(TitleId)) throw new InvalidOperationException("This event-based title is not supported.");
                    licensedIds = (await EventCatalog.GetTitleAsync(TitleId, CancellationToken.None)).AchievementIds.ToHashSet();
                }

                // Step 3: Get game info for display
                var gameInfo = await GetRestAPI().GetGameTitleAsync(account.XUIDOnly, TitleId);
                var gameName = gameInfo?.Titles?.FirstOrDefault()?.Name ?? $"Title {TitleId}";
                GameName = $"Game: {gameName}" + (isEventBased ? " [Event-Based]" : "");

                // Step 4: Filter only unlocked achievements from reference user, sorted by unlock time
                var refUnlocked = refAchievements.achievements
                    .Where(a => a.progressState == "Achieved" && !string.IsNullOrWhiteSpace(a.progression?.timeUnlocked))
                    .OrderBy(a =>
                    {
                        if (DateTime.TryParse(a.progression!.timeUnlocked, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                            return dt;
                        return DateTime.MaxValue;
                    })
                    .ToList();

                if (refUnlocked.Count == 0)
                {
                    _snackbarService.Show("Error", "Reference user has no unlocked achievements for this title.",
                        NoticeAppearance.Danger, new NoticeIcon(NoticeSymbol.ErrorCircle24), _snackbarDuration);
                    StatusText = "No unlocked achievements on reference user.";
                    IsConfigEnabled = true;
                    return;
                }

                // Step 5: Fetch current user's achievements to know which are already unlocked
                StatusText = "Fetching your achievements...";
                var myAchievements = await GetRestAPI().GetAchievementsForTitleAsync(account.XUIDOnly, TitleId);
                var myUnlockedIds = new HashSet<string>();
                if (myAchievements != null)
                {
                    foreach (var a in myAchievements.achievements.Where(a => a.progressState == "Achieved"))
                        myUnlockedIds.Add(a.id);
                }

                // Step 6: Build the queue with time deltas
                // For event-based games, load the event data to check which achievements are supported
                var queue = new List<AutoUnlockQueueEntry>();
                DateTime? previousUnlockTime = null;

                foreach (var achievement in refUnlocked)
                {
                    // Skip achievements already unlocked by current user
                    if (myUnlockedIds.Contains(achievement.id))
                        continue;

                    // For event-based games, skip achievements that don't have event data
                    if (isEventBased && !licensedIds.Contains(achievement.id)) continue;

                    DateTime unlockTime = DateTime.MinValue;
                    if (DateTime.TryParse(achievement.progression!.timeUnlocked, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                        unlockTime = dt;

                    double delaySeconds = 0;
                    if (previousUnlockTime.HasValue && unlockTime > previousUnlockTime.Value)
                    {
                        var referenceDelay = (unlockTime - previousUnlockTime.Value).TotalSeconds;
                        delaySeconds = AddReferenceTimingVariation(referenceDelay);
                    }

                    // Parse gamerscore - check direct field first, then rewards list
                    int gamerscore = 0;
                    if (achievement.gamerscore != null)
                    {
                        if (achievement.gamerscore is long l) gamerscore = (int)l;
                        else if (achievement.gamerscore is int i) gamerscore = i;
                        else int.TryParse(achievement.gamerscore.ToString(), out gamerscore);
                    }
                    if (gamerscore == 0 && achievement.rewards != null)
                    {
                        var gsReward = achievement.rewards.FirstOrDefault(r =>
                            string.Equals(r.type, "Gamerscore", StringComparison.OrdinalIgnoreCase));
                        if (gsReward != null)
                            int.TryParse(gsReward.value, out gamerscore);
                    }

                    queue.Add(new AutoUnlockQueueEntry
                    {
                        AchievementId = achievement.id,
                        AchievementName = achievement.name,
                        Gamerscore = gamerscore,
                        DelaySeconds = delaySeconds,
                        Completed = false
                    });

                    previousUnlockTime = unlockTime;
                }

                if (queue.Count == 0)
                {
                    _snackbarService.Show("All Done", "All of the reference user's achievements are already unlocked on your account.",
                        NoticeAppearance.Success, new NoticeIcon(NoticeSymbol.Checkmark24), _snackbarDuration);
                    StatusText = "Nothing to unlock.";
                    IsConfigEnabled = true;
                    return;
                }

                // Step 7: Save state
                _state = new AutoUnlockState
                {
                    ReferenceGamertag = refGamertagResolved,
                    ReferenceXuid = refXuid,
                    TitleId = TitleId,
                    GameName = gameName,
                    ServiceConfigId = serviceConfigId,
                    Queue = queue,
                    CurrentIndex = 0,
                    RemainingDelaySeconds = queue[0].DelaySeconds,
                    IsRunning = false,
                    SpeedMultiplier = SpeedMultiplier,
                    UseFakeSignature = account.Settings.FakeSignatureEnabled,
                    IsEventBased = isEventBased
                };
                _state.Save();

                PopulateQueueDisplay();
                UpdateProgressText();

                StatusText = $"Queue built: {queue.Count} achievements to unlock. Press Start to begin.";
                IsConfigEnabled = true;
                HasExistingState = true;
                ExistingStateInfo = $"Saved session: {gameName} ({refGamertagResolved}) - 0/{queue.Count} completed";

                _snackbarService.Show("Queue Built",
                    $"{queue.Count} achievements queued for auto unlock.",
                    NoticeAppearance.Success, new NoticeIcon(NoticeSymbol.Checkmark24), _snackbarDuration);
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
                IsConfigEnabled = true;
                _snackbarService.Show("Error", $"Failed to build queue: {ex.Message}",
                    NoticeAppearance.Danger, new NoticeIcon(NoticeSymbol.ErrorCircle24), _snackbarDuration);
            }
        }

        [RelayCommand]
        public async Task StartStopAutoUnlock()
        {
            if (IsRunning)
            {
                // Stop
                _cancellationTokenSource?.Cancel();
                IsRunning = false;
                IsConfigEnabled = true;
                StartStopButtonText = "Start Auto Unlock";
                StatusText = "Stopped.";
                if (_state != null)
                {
                    _state.IsRunning = false;
                    _state.Save();
                }
                return;
            }

            if (_state == null)
            {
                _snackbarService.Show("Error", "Build a queue first before starting.",
                    NoticeAppearance.Danger, new NoticeIcon(NoticeSymbol.ErrorCircle24), _snackbarDuration);
                return;
            }

            await StartAutoUnlockProcess();
        }

        private async Task StartAutoUnlockProcess()
        {
            if (_state == null) return;

            _state.SpeedMultiplier = SpeedMultiplier;
            IsRunning = true;
            IsConfigEnabled = false;
            StartStopButtonText = "Stop Auto Unlock";
            _state.IsRunning = true;
            _state.Save();

            // Start spoofing - send initial heartbeat so it shows as playing the game
            account.SpoofedTitleID = _state.TitleId;
            account.SpoofingStatus = 1;
            var hbResult = await GetRestAPI().SendHeartbeatAsync(account.XUIDOnly, _state.TitleId);
            if (hbResult.StatusCode < 200 || hbResult.StatusCode >= 300)
            {
                StatusText = $"Heartbeat failed: HTTP {hbResult.StatusCode} - {(hbResult.Body.Length > 150 ? hbResult.Body.Substring(0, 150) : hbResult.Body)}";
            }

            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            try
            {
                await Task.Run(async () => await RunAutoUnlockLoop(token), token);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopped
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
                _snackbarService.Show("Error", $"Auto unlock error: {ex.Message}",
                    NoticeAppearance.Danger, new NoticeIcon(NoticeSymbol.ErrorCircle24), _snackbarDuration);
            }
            finally
            {
                // Stop spoofing
                await GetRestAPI().StopHeartbeatAsync(account.XUIDOnly);
                account.SpoofingStatus = 0;
                account.SpoofedTitleID = "0";

                IsRunning = false;
                IsConfigEnabled = true;
                StartStopButtonText = "Start Auto Unlock";
                if (_state != null)
                {
                    _state.IsRunning = false;
                    _state.Save();
                }
            }
        }

        private async Task RunAutoUnlockLoop(CancellationToken token)
        {
            int heartbeatCounter = 0; // Counts seconds since last heartbeat

            while (_state != null && _state.CurrentIndex < _state.Queue.Count)
            {
                token.ThrowIfCancellationRequested();

                var entry = _state.Queue[_state.CurrentIndex];

                if (entry.Completed)
                {
                    _state.CurrentIndex++;
                    _state.Save();
                    continue;
                }

                // Calculate adjusted delay
                double adjustedDelay = _state.RemainingDelaySeconds / (_state.SpeedMultiplier > 0 ? _state.SpeedMultiplier : 1.0);

                // Update UI
                Avalonia.Threading.Dispatcher.UIThread.Invoke(() =>
                {
                    NextUnlockText = $"Next Unlock: {entry.AchievementName} ({entry.Gamerscore}G)";
                    UpdateProgressText();
                });

                // Wait with countdown
                if (adjustedDelay > 0)
                {
                    var delayEnd = DateTime.UtcNow.AddSeconds(adjustedDelay);

                    while (DateTime.UtcNow < delayEnd)
                    {
                        token.ThrowIfCancellationRequested();

                        var remaining = delayEnd - DateTime.UtcNow;
                        Avalonia.Threading.Dispatcher.UIThread.Invoke(() =>
                        {
                            TimeRemainingText = $"Time Until Next: {remaining:hh\\:mm\\:ss}";
                            StatusText = $"Waiting to unlock: {entry.AchievementName}...";
                        });

                        // Save remaining delay periodically (every 10s) for resume capability
                        _state.RemainingDelaySeconds = remaining.TotalSeconds * (_state.SpeedMultiplier > 0 ? _state.SpeedMultiplier : 1.0);
                        if ((int)remaining.TotalSeconds % 10 == 0)
                        {
                            _state.Save();
                        }

                        // Re-send heartbeat every 300 seconds to keep presence alive
                        heartbeatCounter++;
                        if (heartbeatCounter >= 300)
                        {
                            await GetRestAPI().SendHeartbeatAsync(account.XUIDOnly, _state.TitleId);
                            heartbeatCounter = 0;
                        }

                        await Task.Delay(1000, token);
                    }
                }

                // Unlock the achievement
                Avalonia.Threading.Dispatcher.UIThread.Invoke(() =>
                {
                    StatusText = $"Unlocking: {entry.AchievementName}...";
                    TimeRemainingText = "Time Until Next: Unlocking...";
                });

                bool unlockSuccess = false;
                string unlockError = "";

                try
                {
                    if (_state.IsEventBased)
                    {
                        unlockSuccess = await UnlockEventBasedAchievementAsync(entry.AchievementId);
                        if (!unlockSuccess)
                            unlockError = "Event-based unlock failed (see snackbar for details)";
                    }
                    else
                    {
                        await GetRestAPI().UnlockTitleBasedAchievementAsync(
                            _state.ServiceConfigId,
                            _state.TitleId,
                            account.XUIDOnly,
                            entry.AchievementId,
                            _state.UseFakeSignature);
                        unlockSuccess = true;
                    }
                }
                catch (Exception ex)
                {
                    unlockError = ex.Message;
                }

                if (unlockSuccess)
                {
                    entry.Completed = true;
                    _state.CurrentIndex++;

                    // Set remaining delay for next item
                    if (_state.CurrentIndex < _state.Queue.Count)
                    {
                        _state.RemainingDelaySeconds = _state.Queue[_state.CurrentIndex].DelaySeconds;
                    }

                    _state.Save();

                    Avalonia.Threading.Dispatcher.UIThread.Invoke(() =>
                    {
                        UpdateProgressText();
                        UpdateQueueItemStatus(_state.CurrentIndex - 1, "Unlocked");
                        _snackbarService.Show("Achievement Unlocked",
                            $"{entry.AchievementName} ({entry.Gamerscore}G) has been unlocked.",
                            NoticeAppearance.Success, new NoticeIcon(NoticeSymbol.Checkmark24), _snackbarDuration);
                    });
                }
                else
                {
                    Avalonia.Threading.Dispatcher.UIThread.Invoke(() =>
                    {
                        StatusText = $"Failed to unlock {entry.AchievementName}: {unlockError}";
                        UpdateQueueItemStatus(_state.CurrentIndex, "Failed");
                        _snackbarService.Show("Unlock Failed",
                            $"Failed to unlock {entry.AchievementName}: {unlockError}",
                            NoticeAppearance.Danger, new NoticeIcon(NoticeSymbol.ErrorCircle24), _snackbarDuration);
                    });

                    // Skip failed achievement and continue
                    _state.CurrentIndex++;
                    if (_state.CurrentIndex < _state.Queue.Count)
                    {
                        _state.RemainingDelaySeconds = _state.Queue[_state.CurrentIndex].DelaySeconds;
                    }
                    _state.Save();
                }
            }

            // All done
            Avalonia.Threading.Dispatcher.UIThread.Invoke(() =>
            {
                StatusText = "Auto unlock complete!";
                NextUnlockText = "Next Unlock: N/A";
                TimeRemainingText = "Time Until Next: N/A";
                UpdateProgressText();
                _snackbarService.Show("Complete",
                    "All queued achievements have been processed.",
                    NoticeAppearance.Success, new NoticeIcon(NoticeSymbol.Checkmark24), _snackbarDuration);
            });
        }

        private void PopulateQueueDisplay()
        {
            if (_state == null) return;

            var items = new ObservableCollection<AutoUnlockQueueDisplay>();
            for (int i = 0; i < _state.Queue.Count; i++)
            {
                var entry = _state.Queue[i];
                var item = new AutoUnlockQueueDisplay
                {
                    Index = i + 1,
                    AchievementName = entry.AchievementName,
                    AchievementId = entry.AchievementId,
                    Gamerscore = entry.Gamerscore,
                    CanEditDelay = true,
                    DelayMinutes = (decimal)(entry.DelaySeconds / 60.0),
                    Status = entry.Completed ? "Unlocked" : (i < _state.CurrentIndex ? "Skipped" : "Pending")
                };
                item.DelayChanged = UpdateQueueDelay;
                items.Add(item);
            }
            QueueItems = items;
        }

        private void UpdateProgressText()
        {
            if (_state == null)
            {
                ProgressText = "Progress: 0/0";
                return;
            }

            var completed = _state.Queue.Count(q => q.Completed);
            ProgressText = $"Progress: {completed}/{_state.Queue.Count}";
        }

        private void UpdateQueueItemStatus(int index, string status)
        {
            if (index >= 0 && index < QueueItems.Count)
            {
                // We need to replace the item since the class isn't observable
                var item = QueueItems[index];
                QueueItems[index] = new AutoUnlockQueueDisplay
                {
                    Index = item.Index,
                    AchievementName = item.AchievementName,
                    AchievementId = item.AchievementId,
                    Gamerscore = item.Gamerscore,
                    CanEditDelay = item.CanEditDelay,
                    DelayMinutes = item.DelayMinutes,
                    DelayChanged = UpdateQueueDelay,
                    Status = status
                };
            }
        }

        private static double AddReferenceTimingVariation(double referenceDelaySeconds)
        {
            if (referenceDelaySeconds <= 0) return 0;

            var maximumVariationSeconds = referenceDelaySeconds >= TimeSpan.FromHours(1).TotalSeconds
                ? TimeSpan.FromMinutes(5).TotalSeconds
                : TimeSpan.FromMinutes(1).TotalSeconds;
            var signedVariation = ((Random.Shared.NextDouble() * 2.0) - 1.0) * maximumVariationSeconds;
            return Math.Max(1, referenceDelaySeconds + signedVariation);
        }

        private void UpdateQueueDelay(int queueIndex, decimal delayMinutes)
        {
            if (_state == null || IsRunning || queueIndex < 0 || queueIndex >= _state.Queue.Count) return;

            var delaySeconds = (double)delayMinutes * 60.0;
            _state.Queue[queueIndex].DelaySeconds = delaySeconds;
            if (_state.CurrentIndex == queueIndex)
                _state.RemainingDelaySeconds = delaySeconds;
            _state.Save();
        }

        public void UpdateSpeed(double newSpeed)
        {
            SpeedMultiplier = newSpeed;
            if (_state != null)
            {
                _state.SpeedMultiplier = newSpeed;
                _state.Save();
            }
            UpdateSpeedDisplay();
        }

        private void UpdateSpeedDisplay()
        {
            var label = SpeedMultiplier switch
            {
                1.0 => "Real Time",
                _ when SpeedMultiplier < 1.0 => $"{1.0 / SpeedMultiplier:F1}x Slower",
                _ => $"{SpeedMultiplier:F1}x Faster"
            };
            SpeedDisplay = $"Speed: {SpeedMultiplier:F1}x ({label})";
        }

        #region Event-Based Unlock

        /// <summary>
        /// Unlocks a single event-based achievement by building and sending telemetry request(s).
        /// Returns true if all requests succeeded.
        /// </summary>
        private async Task<bool> UnlockEventBasedAchievementAsync(string achievementId)
        {
            if (_state == null) return false;

            var eventsToken = account.EventsToken;
            if (string.IsNullOrWhiteSpace(eventsToken))
            {
                Avalonia.Threading.Dispatcher.UIThread.Invoke(() =>
                {
                    _snackbarService.Show("Error: No Events Token",
                        "Event-based unlocking requires an events token. Use OAuth login or set it manually.",
                        NoticeAppearance.Danger, new NoticeIcon(NoticeSymbol.ErrorCircle24), _snackbarDuration);
                });
                return false;
            }

            if (EventCatalog == null) return false;
            var requestBodies = (await EventCatalog.GetPayloadsAsync(_state.TitleId, achievementId, account.XUIDOnly, CancellationToken.None)).Payloads.ToList();

            // Send all request(s)
            for (int reqIdx = 0; reqIdx < requestBodies.Count; reqIdx++)
            {
                var content = new StringContent(requestBodies[reqIdx], Encoding.UTF8, "application/x-json-stream");
                var (statusCode, responseBody) = await GetRestAPI().UnlockEventBasedAchievement(eventsToken, content);

                if (statusCode < 200 || statusCode >= 300)
                {
                    var truncated = responseBody.Length > 200 ? responseBody.Substring(0, 200) + "..." : responseBody;
                    Avalonia.Threading.Dispatcher.UIThread.Invoke(() =>
                    {
                        _snackbarService.Show($"Error: Request {reqIdx + 1}/{requestBodies.Count} HTTP {statusCode}",
                            truncated, NoticeAppearance.Danger,
                            new NoticeIcon(NoticeSymbol.ErrorCircle24), TimeSpan.FromSeconds(5));
                    });
                    return false;
                }
            }

            return true;
        }

        private bool ApplyReplacement(ref string requestbody, dynamic ReplacementData)
        {
            switch (ReplacementData.ReplacementType.ToString())
            {
                case "Replace":
                    requestbody = requestbody.Replace(ReplacementData.Target.ToString(), ReplacementData.Replacement.ToString());
                    return true;
                case "RangeInt":
                    {
                        int min = ReplacementData.Min;
                        int max = ReplacementData.Max;
                        Random random = new Random();
                        int randomint = random.Next(min, max);
                        requestbody = requestbody.Replace(ReplacementData.Target.ToString(), randomint.ToString());
                        return true;
                    }
                case "RangeFloat":
                    {
                        float min = ReplacementData.Min;
                        float max = ReplacementData.Max;
                        Random random = new Random();
                        float randomfloat = (float)random.NextDouble() * (max - min) + min;
                        requestbody = requestbody.Replace(ReplacementData.Target.ToString(), randomfloat.ToString());
                        return true;
                    }
                case "StupidFuckingLDAPTimestamp":
                    {
                        long ldapTimestamp = DateTime.Now.ToFileTime();
                        requestbody = requestbody.Replace(ReplacementData.Target.ToString(), ldapTimestamp.ToString());
                        return true;
                    }
                default:
                    Avalonia.Threading.Dispatcher.UIThread.Invoke(() =>
                    {
                        _snackbarService.Show("Error: Bad Achievement Data",
                            "Unknown replacement type in event data",
                            NoticeAppearance.Danger, new NoticeIcon(NoticeSymbol.ErrorCircle24), _snackbarDuration);
                    });
                    return false;
            }
        }

        private string ApplyCommonReplacements(string requestbody, DateTime timestamp)
        {
            requestbody = requestbody.Replace("REPLACETIME", timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ"));
            requestbody = requestbody.Replace("REPLACESEQ", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
            requestbody = requestbody.Replace("REPLACEXUID", account.XUIDOnly);
            try
            {
                return JObject.Parse(requestbody).ToString(Formatting.None);
            }
            catch
            {
                return requestbody;
            }
        }

        #endregion
    }
}
