namespace AchievementLabs.Services.LegacyXbox.Win8;

public sealed record Win8AchievementDiagnostic(
    int Id,
    string DisplayName,
    bool LocallySatisfied,
    string Evidence);
