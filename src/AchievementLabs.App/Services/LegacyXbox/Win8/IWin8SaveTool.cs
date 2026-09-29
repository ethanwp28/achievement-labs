namespace AchievementLabs.Services.LegacyXbox.Win8;

public interface IWin8SaveTool
{
    string GameKey { get; }
    string DisplayName { get; }
    bool CanPrepare { get; }
    int MaxAchievementId { get; }

    string GetPrimarySavePath(long xuid);
    Win8SaveSnapshot Read(long xuid);
    string Backup(long xuid);
    Win8SavePatchResult PrepareAchievement(long xuid, int achievementId);
    Win8SavePatchResult PrepareAll(long xuid);
}
