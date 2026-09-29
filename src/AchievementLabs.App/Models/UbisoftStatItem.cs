namespace AchievementLabs.Models;

public partial class UbisoftStatItem : ObservableObject
{
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _value = string.Empty;
    [ObservableProperty] private string _source = string.Empty;
    [ObservableProperty] private string _status = "Read-only";
}
