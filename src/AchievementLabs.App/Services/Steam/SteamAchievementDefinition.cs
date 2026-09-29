namespace AchievementLabs.Services.Steam;

public sealed record SteamAchievementDefinition(
    string Id,
    string Name,
    string Description,
    string IconNormal,
    string IconLocked,
    bool IsHidden,
    int Permission);
