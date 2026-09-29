using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace AchievementLabs.Services.LegacyXbox.Win8;

public sealed class Fishdom3SaveTool : IWin8SaveTool
{
    private const string PackageFamilyName = "Microsoft.Fishdom3SpecialEdition_8wekyb3d8bbwe";

    private static readonly FishdomAchievementDefinition[] Definitions =
    [
        new(1, 5, "Thunderer", "Use lightning 50 times", 50),
        new(2, 6, "Hunting for pieces", "Remove 1,000 pieces on a single level", 1000),
        new(3, 7, "Archaeologist", "Collect 100 artifacts", 100),
        new(4, 9, "Gold rush", "Collect 500 units of gold and diamonds", 500),
        new(5, 12, "Limits of possibility", "Create 10 matches in 10 seconds", 10),
        new(6, 14, "Chain reaction", "Make a chain of 3 explosive devices", 3),
        new(7, 15, "True to your vocation", "Complete 200 levels", 200),
        new(8, 16, "Tactician", "Clear off 120 pieces in one move", 120),
        new(9, 18, "Temporary Insanity", "Use all explosive types on one level", 4),
        new(10, 22, "Inexhaustible Vein", "Earn 1,000,000 coins", 1_000_000),
        new(11, 23, "Good student", "Reach Level 25", 25),
        new(12, 24, "Experienced designer", "Spend 10,000 coins decorating", 10_000),
        new(13, 25, "Broad spectrum", "Get all backgrounds", 8),
        new(14, 28, "Great aquarist", "Get 10 aquariums", 10),
        new(15, 30, "For loyalty", "Play for an hour", 1),
        new(16, 31, "King of the Turtles", "Buy 10 turtles", 10),
        new(17, 49, "Perfect aquarium", "Highest trophy for 5 aquariums", 5),
        new(18, 50, "Strong stamina", "Unlock bonus levels", 1),
        new(19, 52, "Time is money", "Complete 100 golden-time levels", 100),
        new(20, 20, "Ark", "Buy one fish of each species", 18)
    ];

    public string GameKey => "fishdom3";
    public string DisplayName => "Fishdom 3: Special Edition";
    public bool CanPrepare => true;
    public int MaxAchievementId => 20;

    public string GetPrimarySavePath(long xuid)
    {
        return Path.Combine(GetPackagePath(), "RoamingState", $"{xuid}.xml");
    }

    public Win8SaveSnapshot Read(long xuid)
    {
        var savePath = GetPrimarySavePath(xuid);
        if (!File.Exists(savePath))
            return Win8SaveSnapshot.Missing(DisplayName, savePath, xuid, "Fishdom roaming save was not found. Launch Fishdom once with this Xbox profile, then refresh.");

        var document = ReadSaveDocument(savePath, out var uncompressedLength);
        var achievements = BuildDiagnostics(document);
        var files = CollectFiles(xuid);

        return new Win8SaveSnapshot(
            DisplayName,
            savePath,
            xuid,
            true,
            $"Compressed XML, {uncompressedLength} bytes decompressed",
            $"{achievements.Count(a => a.LocallySatisfied)}/{achievements.Count} local achievement triggers prepared.",
            achievements,
            files);
    }

    public string Backup(long xuid)
    {
        var savePath = GetRequiredSavePath(xuid);
        return BackupFile(savePath);
    }

    public Win8SavePatchResult PrepareAchievement(long xuid, int achievementId)
    {
        var definition = Definitions.FirstOrDefault(item => item.XboxId == achievementId)
            ?? throw new ArgumentOutOfRangeException(nameof(achievementId), "Unknown Fishdom achievement ID.");

        var savePath = GetRequiredSavePath(xuid);
        var document = ReadSaveDocument(savePath, out _);
        var changed = new List<string>();
        ApplyDefinition(document, definition, changed);

        if (changed.Count == 0)
            return new Win8SavePatchResult(savePath, string.Empty, [achievementId], []);

        var backupPath = BackupFile(savePath);
        WriteSaveDocument(savePath, document);
        return new Win8SavePatchResult(savePath, backupPath, [achievementId], changed);
    }

    public Win8SavePatchResult PrepareAll(long xuid)
    {
        var savePath = GetRequiredSavePath(xuid);
        var document = ReadSaveDocument(savePath, out _);
        var changed = new List<string>();

        foreach (var definition in Definitions)
            ApplyDefinition(document, definition, changed);

        if (changed.Count == 0)
            return new Win8SavePatchResult(savePath, string.Empty, Definitions.Select(item => item.XboxId).ToList(), []);

        var backupPath = BackupFile(savePath);
        WriteSaveDocument(savePath, document);
        return new Win8SavePatchResult(
            savePath,
            backupPath,
            Definitions.Select(item => item.XboxId).ToList(),
            changed.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static void ApplyDefinition(XDocument document, FishdomAchievementDefinition definition, List<string> changed)
    {
        SetInternalAchievementProgress(document, definition, changed);

        switch (definition.XboxId)
        {
            case 11:
                SetDataElem(document, "Rank", "int", "25", changed);
                break;
            case 13:
                SetAllAttributes(document, "backgrounds", "bought", "true", changed);
                break;
            case 14:
                SetTankCount(document, 10, changed);
                break;
            case 17:
                SetPerfectTankCups(document, 5, changed);
                break;
            case 20:
                SetAllFishBought(document, changed);
                break;
        }
    }

    private static IReadOnlyList<Win8AchievementDiagnostic> BuildDiagnostics(XDocument document)
    {
        return Definitions
            .Select(definition =>
            {
                var progress = GetInternalProgress(document, definition.InternalId);
                return new Win8AchievementDiagnostic(
                    definition.XboxId,
                    definition.DisplayName,
                    progress >= definition.Threshold,
                    $"internal {definition.InternalId} progress = {progress} / {definition.Threshold}; {definition.Trigger}");
            })
            .ToList();
    }

    private static int GetInternalProgress(XDocument document, int internalId)
    {
        var achievement = GetAchievementsElement(document)?
            .Elements("Achievement")
            .FirstOrDefault(element => (string?)element.Attribute("id") == internalId.ToString(CultureInfo.InvariantCulture));

        return int.TryParse((string?)achievement?.Attribute("progress"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var progress)
            ? progress
            : 0;
    }

    private static void SetInternalAchievementProgress(XDocument document, FishdomAchievementDefinition definition, List<string> changed)
    {
        var achievements = GetOrCreateAchievementsElement(document);
        var idText = definition.InternalId.ToString(CultureInfo.InvariantCulture);
        var achievement = achievements
            .Elements("Achievement")
            .FirstOrDefault(element => (string?)element.Attribute("id") == idText);

        if (achievement is null)
        {
            achievements.Add(new XElement("Achievement",
                new XAttribute("id", idText),
                new XAttribute("progress", definition.Threshold.ToString(CultureInfo.InvariantCulture))));
            changed.Add($"{definition.DisplayName}: added internal progress {definition.Threshold}");
            return;
        }

        var current = GetInternalProgress(document, definition.InternalId);
        if (current >= definition.Threshold)
            return;

        achievement.SetAttributeValue("progress", definition.Threshold.ToString(CultureInfo.InvariantCulture));
        changed.Add($"{definition.DisplayName}: progress {current} -> {definition.Threshold}");
    }

    private static XElement? GetAchievementsElement(XDocument document)
    {
        return document.Root?
            .Element("Players")?
            .Element("Player")?
            .Element("Achievements");
    }

    private static XElement GetOrCreateAchievementsElement(XDocument document)
    {
        var player = document.Root?.Element("Players")?.Element("Player")
            ?? throw new InvalidDataException("Fishdom save did not contain a player element.");

        var achievements = player.Element("Achievements");
        if (achievements is not null)
            return achievements;

        achievements = new XElement("Achievements");
        player.Add(achievements);
        return achievements;
    }

    private static void SetDataElem(XDocument document, string name, string type, string value, List<string> changed)
    {
        var player = document.Root?.Element("Players")?.Element("Player")
            ?? throw new InvalidDataException("Fishdom save did not contain a player element.");

        var element = player.Elements("DataElem").FirstOrDefault(item => (string?)item.Attribute("name") == name);
        if (element is null)
        {
            player.AddFirst(new XElement("DataElem",
                new XAttribute("name", name),
                new XAttribute("type", type),
                new XAttribute("value", value)));
            changed.Add($"{name}: added {value}");
            return;
        }

        var previous = (string?)element.Attribute("value") ?? string.Empty;
        if (previous == value)
            return;

        element.SetAttributeValue("type", type);
        element.SetAttributeValue("value", value);
        changed.Add($"{name}: {previous} -> {value}");
    }

    private static void SetAllAttributes(XDocument document, string ancestorName, string attributeName, string value, List<string> changed)
    {
        var ancestor = document.Descendants(ancestorName).FirstOrDefault();
        if (ancestor is null)
            return;

        foreach (var element in ancestor.Descendants().Where(item => item.Attribute(attributeName) is not null))
        {
            var previous = (string?)element.Attribute(attributeName) ?? string.Empty;
            if (previous == value)
                continue;

            element.SetAttributeValue(attributeName, value);
            changed.Add($"{element.Name.LocalName}.{attributeName}: {previous} -> {value}");
        }
    }

    private static void SetTankCount(XDocument document, int count, List<string> changed)
    {
        var tanks = document.Descendants("TankInfo").OrderBy(tank => (int?)tank.Attribute("id") ?? 0).ToList();
        foreach (var tank in tanks.Take(count))
        {
            var previous = (string?)tank.Attribute("bought") ?? string.Empty;
            if (previous == "true")
                continue;

            tank.SetAttributeValue("bought", "true");
            changed.Add($"Tank {tank.Attribute("id")?.Value}: bought {previous} -> true");
        }
    }

    private static void SetPerfectTankCups(XDocument document, int count, List<string> changed)
    {
        var tanks = document.Descendants("TankInfo").OrderBy(tank => (int?)tank.Attribute("id") ?? 0).ToList();
        foreach (var tank in tanks.Take(count))
        {
            var previous = (string?)tank.Attribute("pulch_cup_complete") ?? string.Empty;
            if (previous == "1")
                continue;

            tank.SetAttributeValue("pulch_cup_complete", "1");
            changed.Add($"Tank {tank.Attribute("id")?.Value}: pulch_cup_complete {previous} -> 1");
        }
    }

    private static void SetAllFishBought(XDocument document, List<string> changed)
    {
        var fishNodes = document.Descendants("FishIsBuyOnce").Descendants("Fish");
        foreach (var fish in fishNodes)
        {
            var previous = (string?)fish.Attribute("isBuyOnce") ?? string.Empty;
            if (previous == "true")
                continue;

            fish.SetAttributeValue("isBuyOnce", "true");
            changed.Add($"Fish {fish.Attribute("id")?.Value}: isBuyOnce {previous} -> true");
        }
    }

    private static XDocument ReadSaveDocument(string savePath, out int uncompressedLength)
    {
        var bytes = File.ReadAllBytes(savePath);
        if (bytes.Length < 6)
            throw new InvalidDataException("Fishdom save is too small to contain the compressed XML payload.");

        uncompressedLength = BitConverter.ToInt32(bytes, 0);
        using var input = new MemoryStream(bytes, 4, bytes.Length - 4);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);

        var xml = Encoding.UTF8.GetString(output.ToArray());
        return XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
    }

    private static void WriteSaveDocument(string savePath, XDocument document)
    {
        var xml = document.ToString(SaveOptions.DisableFormatting);
        var payload = Encoding.UTF8.GetBytes(xml);

        using var output = new MemoryStream();
        output.Write(BitConverter.GetBytes(payload.Length));
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            zlib.Write(payload);

        File.WriteAllBytes(savePath, output.ToArray());
    }

    private string GetRequiredSavePath(long xuid)
    {
        var savePath = GetPrimarySavePath(xuid);
        if (!File.Exists(savePath))
            throw new FileNotFoundException("Fishdom roaming save file was not found for this XUID.", savePath);

        return savePath;
    }

    private static string BackupFile(string path)
    {
        var backupPath = $"{path}.{DateTime.Now:yyyyMMdd-HHmmss}.bak";
        File.Copy(path, backupPath, overwrite: false);
        return backupPath;
    }

    private static string GetPackagePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages",
            PackageFamilyName);
    }

    private static IReadOnlyList<Win8SaveFileInfo> CollectFiles(long xuid)
    {
        var packagePath = GetPackagePath();
        var paths = new[]
        {
            Path.Combine(packagePath, "RoamingState", $"{xuid}.xml"),
            Path.Combine(packagePath, "RoamingState", "log.htm"),
            Path.Combine(packagePath, "LocalState", "sentient.dat")
        };

        return paths.Where(File.Exists).Select(ToFileInfo).ToList();
    }

    private static Win8SaveFileInfo ToFileInfo(string path)
    {
        var info = new FileInfo(path);
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        return new Win8SaveFileInfo(path, info.Length, info.LastWriteTime, hash);
    }

    private sealed record FishdomAchievementDefinition(
        int XboxId,
        int InternalId,
        string DisplayName,
        string Trigger,
        int Threshold);
}
