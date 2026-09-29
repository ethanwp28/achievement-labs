namespace AchievementLabs.Services.LegacyXbox.Ty;

public sealed record TyAchievementDefinition(
    int Id,
    string InternalName,
    string DisplayName,
    string Trigger);
