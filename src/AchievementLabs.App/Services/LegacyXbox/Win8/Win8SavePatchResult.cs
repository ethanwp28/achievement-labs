namespace AchievementLabs.Services.LegacyXbox.Win8;

public sealed record Win8SavePatchResult(
    string SavePath,
    string BackupPath,
    IReadOnlyList<int> PreparedAchievementIds,
    IReadOnlyList<string> ChangedSettings);
