namespace AchievementLabs.Services.LegacyXbox.Ty;

public sealed record TySaveSnapshot(
    string SavePath,
    long Xuid,
    bool Exists,
    bool HashValid,
    int PayloadLength,
    int Version,
    int SettingCount,
    IReadOnlyList<TySaveSetting> Settings,
    IReadOnlyList<TyAchievementDiagnostic> Achievements)
{
    public static TySaveSnapshot Missing(string savePath, long xuid) =>
        new(savePath, xuid, false, false, 0, 0, 0, [], []);
}
