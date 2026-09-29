using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AchievementLabs.Services.LegacyXbox.Win8;

public sealed class MicrosoftBingoLocalSyncTool : IWin8SaveTool
{
    private const string PackageFamilyName = "Microsoft.MicrosoftBingo_8wekyb3d8bbwe";
    private const int TitleId = 1047964830;
    private const string ServiceConfigId = "06430100-7c3e-4160-b1d1-68ab3e76ac9e";

    private static readonly BingoStatDefinition[] Definitions =
    [
        new(1, "Wanderlust", "Complete 25 bingo matches", "MatchCompleted", 25),
        new(2, "Sight Seeker", "Complete 100 bingo matches", "MatchCompleted", 100),
        new(3, "It's in the Bag", "Open 50 surprise boxes", "SurpriseBoxOpened", 50),
        new(4, "Frequent Flyer", "Use 50 powerups", "PowerupUsed", 50),
        new(5, "Jet Setter", "Use 200 powerups", "PowerupUsed", 200),
        new(6, "Cornered", "Call 20 corner bingos", "BingoCornerCalled", 20),
        new(7, "Power to the People", "Daub 500 cells", "CellDaubed", 500),
        new(8, "The Dauber", "Daub 2,000 cells", "CellDaubed", 2000),
        new(9, "First Class", "Reach the first-class XP threshold", "Inventory.Currency_Xp", 1386, IsGuestStat: false),
        new(10, "Around the World", "Reach the around-the-world XP threshold", "Inventory.Currency_Xp", 26152, IsGuestStat: false),
        new(11, "Power Bingo", "Call 30 power bingos", "PowerBingoCalled", 30),
        new(12, "Power Up", "Charge the power meter within 10 seconds", "PowerMeterChargedWithin10Seconds", 1, true),
        new(13, "Big Spender", "Spend 1,000 coins", "CoinSpent", 1000),
        new(14, "High Roller", "Spend 10,000 coins", "CoinSpent", 10000),
        new(15, "Collection Plate", "Complete one collection", "Inventory.Completed_Collection_Count", 1, IsGuestStat: false),
        new(16, "Collector", "Complete three collections", "Inventory.Completed_Collection_Count", 3, IsGuestStat: false),
        new(17, "Line Them Up", "Call four bingos at the same time", "Called4BingoAtTheSameTime", 1, true),
        new(18, "Globe Trotter", "Build a destination streak of 10", "DestinationStreak", 10),
        new(19, "Sightseeing", "Unlock a travel pattern", "UnlockedATravelPattern", 1, true),
        new(20, "Jet Lag", "Have five balls that cannot be daubed", "Had5BallThatCouldNotBeDaubed", 1, true)
    ];

    public string GameKey => "microsoftbingo";
    public string DisplayName => "Microsoft Bingo (Windows 10)";
    public bool CanPrepare => true;
    public int MaxAchievementId => 20;

    public string GetPrimarySavePath(long xuid)
    {
        return Path.Combine(GetPackagePath(), "LocalState", "GuestStats.json");
    }

    public Win8SaveSnapshot Read(long xuid)
    {
        var savePath = GetPrimarySavePath(xuid);
        var packagePath = GetPackagePath();
        var files = CollectFiles(savePath, packagePath);

        if (!Directory.Exists(packagePath))
        {
            return new Win8SaveSnapshot(
                DisplayName,
                savePath,
                xuid,
                false,
                "Package missing",
                $"Microsoft Bingo package container was not found. Local stat sync needs {PackageFamilyName} installed so the game can run SynchronizeGameAndXboxStats / MergeLocalStats.",
                BuildDiagnostics(new JObject(), fileExists: false),
                files);
        }

        var stats = File.Exists(savePath) ? ReadStats(savePath) : new JObject();
        var diagnostics = BuildDiagnostics(stats, File.Exists(savePath));
        var prepared = diagnostics.Count(item => item.LocallySatisfied);

        return new Win8SaveSnapshot(
            DisplayName,
            savePath,
            xuid,
            File.Exists(savePath),
            File.Exists(savePath) ? "GuestStats.json static reconstruction" : "GuestStats.json not found",
            $"{prepared}/{diagnostics.Count} reconstructed local stat triggers prepared. TitleId {TitleId}, SCID {ServiceConfigId}. Inventory-based achievements are flagged because GuestStats.json does not appear to own those values.",
            diagnostics,
            files);
    }

    public string Backup(long xuid)
    {
        var savePath = GetRequiredPackageSavePath();
        if (!File.Exists(savePath))
            throw new FileNotFoundException("GuestStats.json was not found; there is nothing to back up yet.", savePath);

        return BackupFile(savePath);
    }

    public Win8SavePatchResult PrepareAchievement(long xuid, int achievementId)
    {
        var definition = Definitions.FirstOrDefault(item => item.Id == achievementId)
            ?? throw new ArgumentOutOfRangeException(nameof(achievementId), "Unknown Microsoft Bingo achievement ID.");

        if (!definition.IsGuestStat)
        {
            return new Win8SavePatchResult(
                GetRequiredPackageSavePath(allowMissingFile: true),
                string.Empty,
                [achievementId],
                [$"{definition.Name}: {definition.StatName} is inventory/server state, not confirmed in GuestStats.json."]);
        }

        var savePath = GetRequiredPackageSavePath(allowMissingFile: true);
        var stats = File.Exists(savePath) ? ReadStats(savePath) : new JObject();
        var changed = new List<string>();
        ApplyDefinition(stats, definition, changed);

        if (changed.Count == 0)
            return new Win8SavePatchResult(savePath, string.Empty, [achievementId], []);

        var backupPath = File.Exists(savePath) ? BackupFile(savePath) : string.Empty;
        WriteStats(savePath, stats);
        return new Win8SavePatchResult(savePath, backupPath, [achievementId], changed);
    }

    public Win8SavePatchResult PrepareAll(long xuid)
    {
        var savePath = GetRequiredPackageSavePath(allowMissingFile: true);
        var stats = File.Exists(savePath) ? ReadStats(savePath) : new JObject();
        var changed = new List<string>();

        foreach (var definition in Definitions.Where(item => item.IsGuestStat))
            ApplyDefinition(stats, definition, changed);

        foreach (var definition in Definitions.Where(item => !item.IsGuestStat))
            changed.Add($"{definition.Name}: skipped {definition.StatName}; inventory/server value is not confirmed in GuestStats.json.");

        if (changed.Count == Definitions.Count(item => !item.IsGuestStat))
            return new Win8SavePatchResult(savePath, string.Empty, Definitions.Select(item => item.Id).ToList(), changed);

        var backupPath = File.Exists(savePath) ? BackupFile(savePath) : string.Empty;
        WriteStats(savePath, stats);
        return new Win8SavePatchResult(savePath, backupPath, Definitions.Select(item => item.Id).ToList(), changed);
    }

    private static IReadOnlyList<Win8AchievementDiagnostic> BuildDiagnostics(JObject stats, bool fileExists)
    {
        return Definitions
            .Select(definition =>
            {
                if (!definition.IsGuestStat)
                {
                    return new Win8AchievementDiagnostic(
                        definition.Id,
                        definition.Name,
                        false,
                        $"{definition.Description}. {definition.StatName} threshold {definition.Threshold}; not confirmed in GuestStats.json.");
                }

                var value = GetStatValue(stats, definition.StatName);
                var satisfied = definition.IsBoolean
                    ? value >= 1
                    : value >= definition.Threshold;
                var displayValue = definition.IsBoolean ? (value >= 1 ? "true" : "false") : value.ToString(CultureInfo.InvariantCulture);
                var source = fileExists ? "GuestStats.json" : "candidate GuestStats.json";

                return new Win8AchievementDiagnostic(
                    definition.Id,
                    definition.Name,
                    satisfied,
                    $"{source}: {definition.StatName} = {displayValue} / {(definition.IsBoolean ? "true" : definition.Threshold.ToString(CultureInfo.InvariantCulture))}; {definition.Description}.");
            })
            .ToList();
    }

    private static void ApplyDefinition(JObject stats, BingoStatDefinition definition, List<string> changed)
    {
        if (!definition.IsGuestStat)
            return;

        if (definition.IsBoolean)
        {
            var current = GetStatValue(stats, definition.StatName) >= 1;
            if (current)
                return;

            stats[definition.StatName] = true;
            changed.Add($"{definition.Name}: {definition.StatName} false -> true");
            return;
        }

        var currentValue = GetStatValue(stats, definition.StatName);
        if (currentValue >= definition.Threshold)
            return;

        stats[definition.StatName] = definition.Threshold;
        changed.Add($"{definition.Name}: {definition.StatName} {currentValue} -> {definition.Threshold}");
    }

    private static long GetStatValue(JObject stats, string name)
    {
        var token = stats[name];
        if (token is null)
            return 0;

        return token.Type switch
        {
            JTokenType.Boolean => token.Value<bool>() ? 1 : 0,
            JTokenType.Integer => token.Value<long>(),
            JTokenType.Float => (long)token.Value<double>(),
            JTokenType.String => long.TryParse(token.Value<string>(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0,
            _ => 0
        };
    }

    private static JObject ReadStats(string savePath)
    {
        try
        {
            return JObject.Parse(File.ReadAllText(savePath));
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"GuestStats.json could not be parsed: {ex.Message}", ex);
        }
    }

    private static void WriteStats(string savePath, JObject stats)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
        File.WriteAllText(savePath, stats.ToString(Formatting.Indented));
    }

    private static string GetRequiredPackageSavePath(bool allowMissingFile = false)
    {
        var packagePath = GetPackagePath();
        if (!Directory.Exists(packagePath))
            throw new DirectoryNotFoundException($"Microsoft Bingo package container was not found at {packagePath}. Install/launch the game once before preparing GuestStats.json.");

        var localState = Path.Combine(packagePath, "LocalState");
        Directory.CreateDirectory(localState);
        var savePath = Path.Combine(localState, "GuestStats.json");

        if (!allowMissingFile && !File.Exists(savePath))
            throw new FileNotFoundException("GuestStats.json was not found. Launch Bingo once, then refresh. Achievement Labs can create the file during Prepare if the LocalState folder exists.", savePath);

        return savePath;
    }

    private static string GetPackagePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages",
            PackageFamilyName);
    }

    private static IReadOnlyList<Win8SaveFileInfo> CollectFiles(string savePath, string packagePath)
    {
        var candidates = new List<string>
        {
            savePath,
            Path.Combine(packagePath, "LocalState", "ServerLocalState.json"),
            Path.Combine(packagePath, "LocalState", "IgnoreMergeList.json"),
            Path.Combine(packagePath, "Settings", "settings.dat")
        };

        return candidates
            .Where(File.Exists)
            .Select(ToFileInfo)
            .ToList();
    }

    private static string BackupFile(string path)
    {
        var backupPath = $"{path}.vega-backup-{DateTime.Now:yyyyMMdd-HHmmss}";
        File.Copy(path, backupPath, overwrite: false);
        return backupPath;
    }

    private static Win8SaveFileInfo ToFileInfo(string path)
    {
        var info = new FileInfo(path);
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        return new Win8SaveFileInfo(path, info.Length, info.LastWriteTime, hash);
    }

    private sealed record BingoStatDefinition(
        int Id,
        string Name,
        string Description,
        string StatName,
        long Threshold,
        bool IsBoolean = false,
        bool IsGuestStat = true);
}
