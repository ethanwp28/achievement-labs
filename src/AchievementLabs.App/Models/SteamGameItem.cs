using System.IO;

namespace AchievementLabs.Models;

public partial class SteamGameItem : ObservableObject
{
    [ObservableProperty] private long _appId;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _installPath = string.Empty;
    [ObservableProperty] private string _schemaPath = string.Empty;
    [ObservableProperty] private string _source = string.Empty;
    [ObservableProperty] private string _imageUrl = "pack://application:,,,/Assets/achievement-labs-icon.png";
    [ObservableProperty] private string _achievementSummary = "Open to load";
    [ObservableProperty] private double _progress;

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"App {AppId}" : Name;
    public bool IsInstalled => !string.IsNullOrWhiteSpace(InstallPath) && Directory.Exists(InstallPath);
    public bool HasSchema => !string.IsNullOrWhiteSpace(SchemaPath) && File.Exists(SchemaPath);
    public string Status => $"{(IsInstalled ? "Installed" : "Cached")} / {(HasSchema ? "Schema" : "No schema")}";

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayName));
    }

    partial void OnInstallPathChanged(string value)
    {
        OnPropertyChanged(nameof(IsInstalled));
        OnPropertyChanged(nameof(Status));
    }

    partial void OnSchemaPathChanged(string value)
    {
        OnPropertyChanged(nameof(HasSchema));
        OnPropertyChanged(nameof(Status));
    }
}
