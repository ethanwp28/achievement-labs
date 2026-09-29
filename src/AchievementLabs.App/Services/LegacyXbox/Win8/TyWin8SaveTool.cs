using System.IO;
using System.Security.Cryptography;
using AchievementLabs.Services.LegacyXbox.Ty;

namespace AchievementLabs.Services.LegacyXbox.Win8;

public sealed class TyWin8SaveTool : IWin8SaveTool
{
    private readonly ITySaveReader _reader;

    public TyWin8SaveTool(ITySaveReader reader)
    {
        _reader = reader;
    }

    public string GameKey => "ty";
    public string DisplayName => "TY the Tasmanian Tiger";
    public bool CanPrepare => true;
    public int MaxAchievementId => 20;

    public string GetPrimarySavePath(long xuid) => _reader.GetSavePath(xuid);

    public Win8SaveSnapshot Read(long xuid)
    {
        var snapshot = _reader.Read(xuid);
        if (!snapshot.Exists)
        {
            return Win8SaveSnapshot.Missing(
                DisplayName,
                snapshot.SavePath,
                xuid,
                "TY save file was not found for this XUID. Launch TY once with this Xbox profile, then refresh.");
        }

        var achievements = snapshot.Achievements
            .Select(achievement => new Win8AchievementDiagnostic(
                achievement.Id,
                achievement.DisplayName,
                achievement.LocallySatisfied,
                achievement.Evidence))
            .ToList();

        return new Win8SaveSnapshot(
            DisplayName,
            snapshot.SavePath,
            xuid,
            true,
            snapshot.HashValid ? "Hashed binary save" : "Hashed binary save, invalid SHA512",
            $"Version {snapshot.Version}, payload {snapshot.PayloadLength} bytes, {achievements.Count(a => a.LocallySatisfied)}/{achievements.Count} local achievement triggers prepared.",
            achievements,
            [ToFileInfo(snapshot.SavePath)]);
    }

    public string Backup(long xuid) => _reader.Backup(xuid);

    public Win8SavePatchResult PrepareAchievement(long xuid, int achievementId)
    {
        var result = _reader.PrepareAchievement(xuid, achievementId);
        return new Win8SavePatchResult(
            result.SavePath,
            result.BackupPath,
            result.PreparedAchievementIds,
            result.ChangedSettings);
    }

    public Win8SavePatchResult PrepareAll(long xuid)
    {
        var result = _reader.PrepareAllCounterAchievements(xuid);
        return new Win8SavePatchResult(
            result.SavePath,
            result.BackupPath,
            result.PreparedAchievementIds,
            result.ChangedSettings);
    }

    private static Win8SaveFileInfo ToFileInfo(string path)
    {
        var info = new FileInfo(path);
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        return new Win8SaveFileInfo(path, info.Length, info.LastWriteTime, hash);
    }
}
