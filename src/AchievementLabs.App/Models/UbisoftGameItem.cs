using System.IO;

namespace AchievementLabs.Models;

public partial class UbisoftGameItem : ObservableObject
{
    [ObservableProperty] private string _gameId = string.Empty;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _installPath = string.Empty;
    [ObservableProperty] private string _source = string.Empty;
    [ObservableProperty] private string _launchUri = string.Empty;
    [ObservableProperty] private string _imageUrl = "pack://application:,,,/Assets/achievement-labs-icon.png";
    [ObservableProperty] private string _achievementSummary = "Ubisoft service support pending";
    [ObservableProperty] private double _progress;

    public string Title => string.IsNullOrWhiteSpace(DisplayName)
        ? FirstNonEmpty(Path.GetFileName(InstallPath), $"Ubisoft game {GameId}", "Unknown Ubisoft title")
        : DisplayName;

    public bool IsInstalled => !string.IsNullOrWhiteSpace(InstallPath) && Directory.Exists(InstallPath);
    public string Status => $"{(IsInstalled ? "Installed" : "Registry/cache only")} / {Source}";

    partial void OnDisplayNameChanged(string value) => OnPropertyChanged(nameof(Title));
    partial void OnGameIdChanged(string value) => OnPropertyChanged(nameof(Title));
    partial void OnInstallPathChanged(string value)
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(IsInstalled));
        OnPropertyChanged(nameof(Status));
    }

    partial void OnSourceChanged(string value) => OnPropertyChanged(nameof(Status));

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
