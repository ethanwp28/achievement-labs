using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace AchievementLabs.Services.LegacyXbox.Win8;

public sealed class CutTheRopeWin8Tool : IWin8SaveTool
{
    private const string PackageFamilyName = "ZeptoLabUKLimited.CutTheRope_sq9zxnwrk84pj";
    private const string PackageFullName = "ZeptoLabUKLimited.CutTheRope_1.2.0.43_x86__sq9zxnwrk84pj";
    private const int TitleId = 1515264857;
    private const string HexTitleId = "0x5A511B59";
    private const string ContentId = "CF9E2FC2-7E81-71B5-4821-12B4F1ABDBA6";

    private static readonly CutTheRopeAchievementDefinition[] Definitions =
    [
        new(0, "Bronze Scissors", "Collect 50 stars", "a.hd", 50),
        new(1, "Silver Scissors", "Collect 150 stars", "a.zd", 150),
        new(2, "Golden Scissors", "Collect 300 stars", "a.pd", 300),
        new(3, "Rope Cutter", "Cut 100 ropes", "a.xd", 100),
        new(4, "Rope Cutter Maniac", "Cut 800 ropes", "a.yd", 800),
        new(5, "Ultimate Rope Cutter", "Cut 2000 ropes", "a.Fd", 2000),
        new(6, "Bubble Popper", "Pop 50 bubbles", "a.kd", 50),
        new(7, "Bubble Master", "Pop 300 bubbles", "a.jd", 300),
        new(8, "Spider Buster", "Outsmart 40 spiders", "a.Ad", 40),
        new(9, "Spider Tamer", "Outsmart 200 spiders", "a.Cd", 200),
        new(10, "Spider Lover", "Let the spiders steal candy 100 times", "a.Bd", 100),
        new(11, "Weight Loser", "Lose candy 50 times", "a.Gd", 50),
        new(12, "Calorie Minimizer", "Lose candy 200 times", "a.ld", 200),
        new(13, "Quick Finger", "Cut 3 ropes at once", "a.vd", 1),
        new(14, "Master Finger", "Cut 5 ropes at once", "a.td", 1),
        new(15, "Tummy Teaser", "Have Om Nom open his mouth 10 times in a row", "a.Ed", 1),
        new(16, "Candy Juggler", "Keep the candy in the air for 30 seconds without ropes or bubbles", "a.md", 1),
        new(17, "Romantic Soul", "Reunite 100 candies", "a.wd", 100),
        new(18, "Magician", "Drop candy into magic hats 200 times", "a.sd", 200)
    ];

    public string GameKey => "cuttherope";
    public string DisplayName => "Cut the Rope";
    public bool CanPrepare => false;
    public int MaxAchievementId => 18;

    public string GetPrimarySavePath(long xuid)
    {
        return GetUserPackagePath();
    }

    public Win8SaveSnapshot Read(long xuid)
    {
        var userPath = GetUserPackagePath();
        var installPath = GetInstallPath();
        var files = CollectFiles(userPath, installPath);
        var installed = Directory.Exists(installPath);
        var userContainerExists = Directory.Exists(userPath);
        var format = installed ? "WinJS package metadata" : "Package missing";
        var summary = installed
            ? $"Package is installed and metadata extracted. Windows Store launch is currently blocked by entitlement error 0x803F8001 for content {ContentId}. TitleId {TitleId} ({HexTitleId}); achievement wrapper calls Microsoft.Xbox.User.unlockAchievementAsync(id)."
            : $"Package payload was not found at {installPath}.";

        if (!userContainerExists)
            summary += " User package container was not found.";

        var diagnostics = Definitions
            .Select(definition => new Win8AchievementDiagnostic(
                definition.Id,
                definition.Name,
                false,
                $"{definition.Description}. Internal key {definition.InternalKey}, threshold {definition.Threshold}. Xbox SDK ID is zero-based: {definition.Id}. Requires the game to launch and sign into Xbox before its own SDK can submit."))
            .ToList();

        return new Win8SaveSnapshot(DisplayName, userPath, xuid, installed || userContainerExists, format, summary, diagnostics, files);
    }

    public string Backup(long xuid)
    {
        var userPath = GetUserPackagePath();
        if (!Directory.Exists(userPath))
            throw new DirectoryNotFoundException("Cut the Rope user package container was not found.");

        var backupDir = Path.Combine(userPath, $"vega-backup-{DateTime.Now:yyyyMMdd-HHmmss}");
        Directory.CreateDirectory(backupDir);

        foreach (var file in Directory.EnumerateFiles(userPath, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(userPath, file);
            if (relative.StartsWith("vega-backup-", StringComparison.OrdinalIgnoreCase))
                continue;

            var target = Path.Combine(backupDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: false);
        }

        return backupDir;
    }

    public Win8SavePatchResult PrepareAchievement(long xuid, int achievementId)
    {
        throw new NotSupportedException("Cut the Rope is metadata-only for now. The installed package is blocked by Windows Store entitlement error 0x803F8001, so no local prepare/unlock path is available.");
    }

    public Win8SavePatchResult PrepareAll(long xuid)
    {
        throw new NotSupportedException("Cut the Rope is metadata-only for now. The installed package is blocked by Windows Store entitlement error 0x803F8001, so no local prepare/unlock path is available.");
    }

    private static string GetUserPackagePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages",
            PackageFamilyName);
    }

    private static string GetInstallPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "WindowsApps",
            PackageFullName);
    }

    private static IReadOnlyList<Win8SaveFileInfo> CollectFiles(string userPath, string installPath)
    {
        var candidates = new List<string>
        {
            Path.Combine(userPath, "Settings", "settings.dat"),
            Path.Combine(userPath, "Settings", "settings.dat.LOG1"),
            Path.Combine(installPath, "AppxManifest.xml"),
            Path.Combine(installPath, "StoreManifest.xml"),
            Path.Combine(installPath, "Package.StoreAssociation.xml"),
            Path.Combine(installPath, "xbl.spa"),
            Path.Combine(installPath, "js", "default.js"),
            Path.Combine(installPath, "js", "xbox", "Achievements.js"),
            Path.Combine(installPath, "js", "xbox", "Player.js"),
            Path.Combine(installPath, "scripts", "ctr.js")
        };

        return candidates
            .Where(File.Exists)
            .Select(ToFileInfo)
            .ToList();
    }

    private static Win8SaveFileInfo ToFileInfo(string path)
    {
        var info = new FileInfo(path);
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        return new Win8SaveFileInfo(path, info.Length, info.LastWriteTime, hash);
    }

    private sealed record CutTheRopeAchievementDefinition(
        int Id,
        string Name,
        string Description,
        string InternalKey,
        int Threshold);
}
