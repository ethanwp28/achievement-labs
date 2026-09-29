namespace AchievementLabs.Models;

public partial class UbisoftChallengeItem : ObservableObject
{
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _progress = string.Empty;
    [ObservableProperty] private string _status = "Read-only";
}
