namespace AchievementLabs.Models;

public partial class UbisoftCrossProgressionItem : ObservableObject
{
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _gameId = string.Empty;
    [ObservableProperty] private string _detectedPlatforms = string.Empty;
    [ObservableProperty] private string _evidence = string.Empty;
    [ObservableProperty] private string _status = "Needs verification";
}
