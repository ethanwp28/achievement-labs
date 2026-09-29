namespace AchievementLabs.Models;

public partial class SteamAutoUnlockQueueItem : ObservableObject
{
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _status = "Queued";
    [ObservableProperty] private int _delaySeconds;
    [ObservableProperty] private DateTime? _targetUnlockTime;
    [ObservableProperty] private DateTime? _unlockedAt;
    [ObservableProperty] private SteamAchievementItem? _achievement;
}
