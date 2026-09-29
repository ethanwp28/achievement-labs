namespace AchievementLabs.Models;

public sealed class SteamProfileSummary
{
    public bool IsSteamRunning { get; init; }
    public bool IsLoggedIn { get; init; }
    public string SteamId { get; init; } = "Unknown";
    public string AccountName { get; init; } = "Unknown";
    public string PersonaName { get; init; } = "Unknown";
    public string SteamLevel { get; init; } = "Profile private/unavailable";
    public string BadgeCount { get; init; } = "Profile private/unavailable";
    public string AvatarUrl { get; init; } = "pack://application:,,,/Assets/achievement-labs-icon.png";
    public int GameCount { get; init; }
    public string SteamPath { get; init; } = string.Empty;
}
