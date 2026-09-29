namespace AchievementLabs.Services.LegacyXbox.Ty;

public sealed record TySavePatchResult(
    string SavePath,
    string BackupPath,
    IReadOnlyList<int> PreparedAchievementIds,
    IReadOnlyList<string> ChangedSettings);
