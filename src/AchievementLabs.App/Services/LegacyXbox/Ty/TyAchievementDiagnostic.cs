namespace AchievementLabs.Services.LegacyXbox.Ty;

public sealed record TyAchievementDiagnostic(
    int Id,
    string InternalName,
    string DisplayName,
    string Trigger,
    bool LocallySatisfied,
    string Evidence);
