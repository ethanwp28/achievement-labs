namespace AchievementLabs.Services.LegacyXbox.Win8;

public sealed record Win8SaveSnapshot(
    string GameName,
    string SavePath,
    long Xuid,
    bool Exists,
    string FormatStatus,
    string Summary,
    IReadOnlyList<Win8AchievementDiagnostic> Achievements,
    IReadOnlyList<Win8SaveFileInfo> Files)
{
    public static Win8SaveSnapshot Missing(string gameName, string savePath, long xuid, string message) =>
        new(gameName, savePath, xuid, false, "Missing", message, [], []);
}
