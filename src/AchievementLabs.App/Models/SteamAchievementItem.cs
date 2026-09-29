namespace AchievementLabs.Models;

public partial class SteamAchievementItem : ObservableObject
{
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private bool _isUnlocked;
    [ObservableProperty] private bool _desiredUnlocked;
    [ObservableProperty] private DateTime? _unlockTime;
    [ObservableProperty] private int _permission;

    public bool IsModified => IsUnlocked != DesiredUnlocked;

    partial void OnDesiredUnlockedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsModified));
    }

    partial void OnIsUnlockedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsModified));
    }
}
