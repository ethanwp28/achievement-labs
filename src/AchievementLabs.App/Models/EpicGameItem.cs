using System.IO;

namespace AchievementLabs.Models;

public partial class EpicGameItem : ObservableObject
{
    [ObservableProperty] private string _appName = string.Empty;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _catalogItemId = string.Empty;
    [ObservableProperty] private string _artifactId = string.Empty;
    [ObservableProperty] private string _namespaceId = string.Empty;
    [ObservableProperty] private string _installPath = string.Empty;
    [ObservableProperty] private string _manifestPath = string.Empty;
    [ObservableProperty] private string _source = string.Empty;
    [ObservableProperty] private string _imageUrl = "pack://application:,,,/Assets/achievement-labs-icon.png";
    [ObservableProperty] private string _achievementSummary = "EOS config required";
    [ObservableProperty] private double _progress;

    public string Title => string.IsNullOrWhiteSpace(DisplayName)
        ? FirstNonEmpty(AppName, ArtifactId, CatalogItemId, "Unknown Epic title")
        : DisplayName;

    public bool IsInstalled => !string.IsNullOrWhiteSpace(InstallPath) && Directory.Exists(InstallPath);
    public string PrimaryId => FirstNonEmpty(CatalogItemId, ArtifactId, AppName, "Unknown");
    public string Status => $"{(IsInstalled ? "Installed" : "Manifest only")} / {Source}";

    partial void OnDisplayNameChanged(string value) => OnPropertyChanged(nameof(Title));
    partial void OnAppNameChanged(string value) => OnPropertyChanged(nameof(Title));
    partial void OnArtifactIdChanged(string value)
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(PrimaryId));
    }

    partial void OnCatalogItemIdChanged(string value) => OnPropertyChanged(nameof(PrimaryId));
    partial void OnInstallPathChanged(string value)
    {
        OnPropertyChanged(nameof(IsInstalled));
        OnPropertyChanged(nameof(Status));
    }

    partial void OnSourceChanged(string value) => OnPropertyChanged(nameof(Status));

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
