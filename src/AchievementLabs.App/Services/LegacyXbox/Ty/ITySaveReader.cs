namespace AchievementLabs.Services.LegacyXbox.Ty;

public interface ITySaveReader
{
    string GetSavePath(long xuid);
    TySaveSnapshot Read(long xuid);
    string Backup(long xuid);
    TySavePatchResult PrepareAchievement(long xuid, int achievementId);
    TySavePatchResult PrepareAllCounterAchievements(long xuid);
}
