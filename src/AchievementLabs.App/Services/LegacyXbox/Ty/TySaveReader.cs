using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AchievementLabs.Services.LegacyXbox.Ty;

public sealed class TySaveReader : ITySaveReader
{
    private const long HashSentinel = 844571511;
    private const string PackageFamilyName = "Microsoft.TYtheTasmanianTiger_8wekyb3d8bbwe";

    private static readonly string[] OrderedSettingNames =
    [
        "MusicVolume", "SoundVolume", "Vibration",
        "KeyboardMappingAction_Up_1", "KeyboardMappingAction_Up_2",
        "KeyboardMappingAction_Down_1", "KeyboardMappingAction_Down_2",
        "KeyboardMappingAction_Left_1", "KeyboardMappingAction_Left_2",
        "KeyboardMappingAction_Right_1", "KeyboardMappingAction_Right_2",
        "KeyboardMappingAction_Jump_1", "KeyboardMappingAction_Jump_2",
        "KeyboardMappingAction_AttackInteract_1", "KeyboardMappingAction_AttackInteract_2",
        "KeyboardMappingAction_AttackSpecial_1", "KeyboardMappingAction_AttackSpecial_2",
        "DistanceRun", "DistanceGlided", "FrillsKilled", "BlueTonguesKilled",
        "DragonsKilled", "GeckosKilled", "GoannasKilled", "NanobotsKilled",
        "RoosKilled", "SkinksKilled", "RhinoBeetlesKilled", "SoldierCrabsKilled",
        "MagpiesKilled", "ReefSharksKilled", "CrocsKilled", "RobocrabsKilled",
        "DamagetypeKillsEarth", "DamagetypeKillsFire", "DamagetypeKillsWater",
        "DamagetypeKillsAir", "DamagetypeKillsAges", "DamagetypeKillsDream",
        "DamagetypeKillsChaos", "DamagetypeKillsVoid", "DamagetypeKillsDeath",
        "RangsThrown", "ChallengesWon", "UnlockedRangs", "UnlockedCostumes",
        "TurkeysCaught", "TimeAttackCompleted", "DangerArena1", "DangerArena2",
        "DangerArena3", "DiveHard1", "DiveHard2", "A4.1.M1", "B4.1.M1",
        "C4.2.M1", "A1.1.L1", "A1.2.L1", "A1.3.M1", "A1_GoobooBerries",
        "A1_KoalaKids", "A2.1.L1", "A2.2.L1", "A2.3.M1", "A2_GoobooBerries",
        "A2_KoalaKids", "A3.1.L1", "A3.2.L1", "A3.3.M1", "A3_GoobooBerries",
        "A3_KoalaKids", "B1.1.L1", "B1.2.L1", "B1.3.M1", "B1_GoobooBerries",
        "B1_KoalaKids", "B2.1.L1", "B2.2.L1", "B2.3.M1", "B2_GoobooBerries",
        "B2_KoalaKids", "B3.1.L1", "B3.2.L1", "B3.3.M1", "B3_GoobooBerries",
        "B3_KoalaKids", "C1.1.L1", "C1.2.L1", "C1.3.M1", "C1_GoobooBerries",
        "C1_KoalaKids", "C2.1.L1", "C2.2.L1", "C2.3.M1", "C2_GoobooBerries",
        "C2_KoalaKids", "C3.1.L1", "C3.2.L1", "C3.3.M1", "C3_GoobooBerries",
        "C3_KoalaKids", "A1Complete", "A2Complete", "A3Complete", "B1Complete",
        "B2Complete", "B3Complete", "C1Complete", "C2Complete", "C3Complete",
        "TotalOpalsCollected"
    ];

    private static readonly TyAchievementDefinition[] AchievementDefinitions =
    [
        new(1, "ThrewARang", "You've Still Got It", "RangsThrown >= 1"),
        new(2, "ZoneACompleted", "Floor Fluffy", "A4.1.M1 >= 1"),
        new(3, "ZoneBCompleted", "Smack Sly", "B4.1.M1 >= 1"),
        new(4, "ZoneCCompleted", "Crunch Cass", "C4.2.M1 >= 1"),
        new(5, "ChallengedAFriend", "Tickets On Yourself", "Challenge flow"),
        new(6, "BeatAFriend", "The Best Extinct Thylacine", "Beat a friend's score"),
        new(7, "CatchATurkey", "You're No Turkey", "TurkeysCaught >= 1"),
        new(8, "CompletedEverything", "No Stone Unturned", "All zone-complete settings"),
        new(9, "Defeat100Frills", "Reptile Round-Up", "FrillsKilled >= 100"),
        new(10, "OpalLoaded", "Opal Loaded", "TotalOpalsCollected >= 50000"),
        new(11, "CompleteDive", "Dive Hard With A Vengeance", "DiveHard1 >= 1 and DiveHard2 >= 1"),
        new(12, "DefeatEachEnemy", "Fossil Collector", "One kill for every tracked enemy"),
        new(13, "ElementalRangs", "Elemental As Anything", "One kill for every damage type"),
        new(14, "PostToALeaderboard", "Name In Lights", "Leaderboard post flow"),
        new(15, "BoughtSixRangs", "Rangtastic", "UnlockedRangs contains six purchasable rangs"),
        new(16, "CompleteDangerArenas", "Danger Is My Middle Name", "DangerArena1/2/3 >= 1"),
        new(17, "GlidedXMeters", "Tiger-tail Glider", "DistanceGlided > 1000"),
        new(18, "RanXMeters", "Marathon Marsupial", "DistanceRun > 10000"),
        new(19, "Won50Challenges", "Totally FanTYstic", "ChallengesWon >= 50"),
        new(20, "CompleteTimeAttack", "In The Nick Of TYme", "TimeAttackCompleted >= 1")
    ];

    public string GetSavePath(long xuid)
    {
        var localState = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages",
            PackageFamilyName,
            "LocalState");

        return Path.Combine(localState, ComputeSaveFileName(xuid));
    }

    public TySaveSnapshot Read(long xuid)
    {
        var savePath = GetSavePath(xuid);
        if (!File.Exists(savePath))
            return TySaveSnapshot.Missing(savePath, xuid);

        var parsed = ParseSave(savePath);
        var hashValid = parsed.StoredHash == ComputeSaveHash(parsed.Bytes, parsed.PayloadLength);
        var achievements = BuildAchievementDiagnostics(parsed.Settings);

        return new TySaveSnapshot(
            savePath,
            xuid,
            true,
            hashValid,
            parsed.PayloadLength,
            parsed.Version,
            parsed.Settings.Count,
            parsed.Settings,
            achievements);
    }

    public string Backup(long xuid)
    {
        var savePath = GetRequiredSavePath(xuid);
        return BackupSave(savePath);
    }

    public TySavePatchResult PrepareAchievement(long xuid, int achievementId)
    {
        var savePath = GetRequiredSavePath(xuid);
        var parsed = ParseSave(savePath);
        var map = parsed.Settings.ToDictionary(setting => setting.Name, StringComparer.OrdinalIgnoreCase);
        var changed = new List<string>();

        ApplyAchievementPatch(achievementId, map, changed);

        if (changed.Count == 0)
            return new TySavePatchResult(savePath, string.Empty, [achievementId], []);

        var backupPath = BackupSave(savePath);
        ReplaceSettings(parsed, map);
        WritePatchedSave(savePath, parsed, changed);
        return new TySavePatchResult(savePath, backupPath, [achievementId], changed);
    }

    public TySavePatchResult PrepareAllCounterAchievements(long xuid)
    {
        var savePath = GetRequiredSavePath(xuid);
        var parsed = ParseSave(savePath);
        var map = parsed.Settings.ToDictionary(setting => setting.Name, StringComparer.OrdinalIgnoreCase);
        var changed = new List<string>();
        var prepared = new List<int>();

        foreach (var achievementId in new[] { 1, 2, 3, 4, 7, 8, 9, 10, 11, 12, 13, 15, 16, 17, 18, 19, 20 })
        {
            ApplyAchievementPatch(achievementId, map, changed);
            prepared.Add(achievementId);
        }

        if (changed.Count == 0)
            return new TySavePatchResult(savePath, string.Empty, prepared, []);

        var backupPath = BackupSave(savePath);
        ReplaceSettings(parsed, map);
        WritePatchedSave(savePath, parsed, changed);
        return new TySavePatchResult(savePath, backupPath, prepared, changed.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static void ReplaceSettings(ParsedTySave parsed, Dictionary<string, TySaveSetting> map)
    {
        parsed.Settings.Clear();
        parsed.Settings.AddRange(map.Values.OrderBy(setting => setting.Index));
    }

    private static string ComputeSaveFileName(long xuid)
    {
        Span<byte> buffer = stackalloc byte[16];
        BitConverter.TryWriteBytes(buffer[..8], HashSentinel);
        BitConverter.TryWriteBytes(buffer[8..], xuid);

        var hash = SHA1.HashData(buffer);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ComputeSaveHash(byte[] bytes, int payloadLength)
    {
        var payload = new byte[payloadLength];
        Array.Copy(bytes, payload, payloadLength);
        BitConverter.GetBytes(HashSentinel).CopyTo(payload, 0);
        return Convert.ToBase64String(SHA512.HashData(payload));
    }

    private static string ReadTrailingHash(byte[] bytes, int payloadLength)
    {
        using var stream = new MemoryStream(bytes);
        using var reader = new BinaryReader(stream, Encoding.UTF8);
        stream.Position = payloadLength;
        return reader.ReadString();
    }

    private static ParsedTySave ParseSave(string savePath)
    {
        var bytes = File.ReadAllBytes(savePath);
        var payloadLength = BitConverter.ToInt32(bytes, 0);
        if (payloadLength <= 0 || payloadLength >= bytes.Length)
            throw new InvalidDataException("The TY save payload length is invalid.");

        var storedHash = ReadTrailingHash(bytes, payloadLength);
        var (version, settings, settingsEndOffset) = ReadSettings(bytes, payloadLength);
        var remainder = bytes[settingsEndOffset..payloadLength];
        return new ParsedTySave(savePath, bytes, payloadLength, storedHash, version, settings, remainder);
    }

    private string GetRequiredSavePath(long xuid)
    {
        var savePath = GetSavePath(xuid);
        if (!File.Exists(savePath))
            throw new FileNotFoundException("TY save file was not found for this XUID.", savePath);

        return savePath;
    }

    private static string BackupSave(string savePath)
    {
        var backupPath = $"{savePath}.{DateTime.Now:yyyyMMdd-HHmmss}.bak";
        File.Copy(savePath, backupPath, overwrite: false);
        return backupPath;
    }

    private static (int Version, List<TySaveSetting> Items, int SettingsEndOffset) ReadSettings(byte[] bytes, int payloadLength)
    {
        using var stream = new MemoryStream(bytes, 0, payloadLength, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8);

        reader.ReadInt32();
        reader.ReadInt32();

        var version = reader.ReadInt32();
        var count = reader.ReadInt32();
        var settings = new List<TySaveSetting>(count);

        for (var index = 0; index < count && stream.Position < payloadLength; index++)
        {
            var hash = reader.ReadInt32();
            var serializedName = version >= 3 ? reader.ReadString() : string.Empty;
            var typeByte = reader.ReadByte();
            var type = (TySettingType)(typeByte & 0x7f);
            var temporary = (typeByte & 0x80) != 0;
            object? value = type switch
            {
                TySettingType.Bool => reader.ReadBoolean(),
                TySettingType.Float => reader.ReadSingle(),
                TySettingType.Int => reader.ReadInt32(),
                TySettingType.String => reader.ReadString(),
                TySettingType.List => ReadStringList(reader),
                TySettingType.UInt => reader.ReadUInt32(),
                _ => throw new InvalidDataException($"Unknown TY setting type '{type}'.")
            };

            var name = !string.IsNullOrWhiteSpace(serializedName)
                ? serializedName
                : index < OrderedSettingNames.Length ? OrderedSettingNames[index] : $"Unknown #{index}";

            settings.Add(new TySaveSetting(index, hash, name, type, temporary, value));
        }

        return (version, settings, (int)stream.Position);
    }

    private static IReadOnlyList<string> ReadStringList(BinaryReader reader)
    {
        var count = reader.ReadInt32();
        var items = new List<string>(count);
        for (var i = 0; i < count; i++)
            items.Add(reader.ReadString());
        return items;
    }

    private static IReadOnlyList<TyAchievementDiagnostic> BuildAchievementDiagnostics(IReadOnlyList<TySaveSetting> settings)
    {
        var map = settings
            .GroupBy(setting => setting.Name)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        return AchievementDefinitions
            .Select(definition =>
            {
                var (satisfied, evidence) = Evaluate(definition.Id, map);
                return new TyAchievementDiagnostic(
                    definition.Id,
                    definition.InternalName,
                    definition.DisplayName,
                    definition.Trigger,
                    satisfied,
                    evidence);
            })
            .ToList();
    }

    private static (bool Satisfied, string Evidence) Evaluate(int id, IReadOnlyDictionary<string, TySaveSetting> map)
    {
        return id switch
        {
            1 => AtLeastUInt(map, "RangsThrown", 1),
            2 => AtLeastInt(map, "A4.1.M1", 1),
            3 => AtLeastInt(map, "B4.1.M1", 1),
            4 => AtLeastInt(map, "C4.2.M1", 1),
            5 => (false, "Triggered by challenge UI flow; no direct save counter found."),
            6 => (false, "Triggered by level-complete friend-score flow; no direct save counter found."),
            7 => AtLeastInt(map, "TurkeysCaught", 1),
            8 => CompletedEverything(map),
            9 => AtLeastUInt(map, "FrillsKilled", 100),
            10 => AtLeastUInt(map, "TotalOpalsCollected", 50000),
            11 => AllAtLeastInt(map, 1, "DiveHard1", "DiveHard2"),
            12 => AllAtLeastUInt(map, 1, "FrillsKilled", "BlueTonguesKilled", "DragonsKilled", "GeckosKilled", "GoannasKilled", "NanobotsKilled", "RoosKilled", "SkinksKilled", "RhinoBeetlesKilled", "SoldierCrabsKilled", "MagpiesKilled", "ReefSharksKilled", "CrocsKilled", "RobocrabsKilled"),
            13 => AllAtLeastUInt(map, 1, "DamagetypeKillsEarth", "DamagetypeKillsFire", "DamagetypeKillsWater", "DamagetypeKillsAir", "DamagetypeKillsAges", "DamagetypeKillsDream", "DamagetypeKillsChaos", "DamagetypeKillsVoid", "DamagetypeKillsDeath"),
            14 => (false, "Triggered by level-complete leaderboard post flow; no direct save counter found."),
            15 => BoughtSixRangs(map),
            16 => AllAtLeastInt(map, 1, "DangerArena1", "DangerArena2", "DangerArena3"),
            17 => GreaterThanFloat(map, "DistanceGlided", 1000),
            18 => GreaterThanFloat(map, "DistanceRun", 10000),
            19 => AtLeastUInt(map, "ChallengesWon", 50),
            20 => AtLeastInt(map, "TimeAttackCompleted", 1),
            _ => (false, "Unknown achievement.")
        };
    }

    private static (bool Satisfied, string Evidence) AtLeastInt(IReadOnlyDictionary<string, TySaveSetting> map, string name, int threshold)
    {
        var value = Convert.ToInt32(GetValue(map, name) ?? 0, CultureInfo.InvariantCulture);
        return (value >= threshold, $"{name} = {value} / {threshold}");
    }

    private static (bool Satisfied, string Evidence) AtLeastUInt(IReadOnlyDictionary<string, TySaveSetting> map, string name, uint threshold)
    {
        var value = Convert.ToUInt32(GetValue(map, name) ?? 0u, CultureInfo.InvariantCulture);
        return (value >= threshold, $"{name} = {value} / {threshold}");
    }

    private static (bool Satisfied, string Evidence) GreaterThanFloat(IReadOnlyDictionary<string, TySaveSetting> map, string name, float threshold)
    {
        var value = Convert.ToSingle(GetValue(map, name) ?? 0f, CultureInfo.InvariantCulture);
        return (value > threshold, $"{name} = {value:0.###} / > {threshold:0.###}");
    }

    private static (bool Satisfied, string Evidence) AllAtLeastInt(IReadOnlyDictionary<string, TySaveSetting> map, int threshold, params string[] names)
    {
        var evidence = names.Select(name => AtLeastInt(map, name, threshold).Evidence).ToList();
        return (names.All(name => AtLeastInt(map, name, threshold).Satisfied), string.Join("; ", evidence));
    }

    private static (bool Satisfied, string Evidence) AllAtLeastUInt(IReadOnlyDictionary<string, TySaveSetting> map, uint threshold, params string[] names)
    {
        var evidence = names.Select(name => AtLeastUInt(map, name, threshold).Evidence).ToList();
        return (names.All(name => AtLeastUInt(map, name, threshold).Satisfied), string.Join("; ", evidence));
    }

    private static (bool Satisfied, string Evidence) CompletedEverything(IReadOnlyDictionary<string, TySaveSetting> map)
    {
        var names = new[] { "A1Complete", "A2Complete", "A3Complete", "B1Complete", "B2Complete", "B3Complete", "C1Complete", "C2Complete", "C3Complete", "A4.1.M1", "B4.1.M1", "C4.2.M1" };
        return AllAtLeastInt(map, 1, names);
    }

    private static (bool Satisfied, string Evidence) BoughtSixRangs(IReadOnlyDictionary<string, TySaveSetting> map)
    {
        var required = new[] { "Hyperang", "Chaosrang", "Deadlyrang", "Cryptorang", "Disruptorang", "Doomerang" };
        var unlocked = GetValue(map, "UnlockedRangs") as IReadOnlyList<string> ?? [];
        var missing = required.Where(rang => !unlocked.Contains(rang, StringComparer.OrdinalIgnoreCase)).ToList();
        return (missing.Count == 0, $"UnlockedRangs = {string.Join(", ", unlocked)}; missing = {string.Join(", ", missing)}");
    }

    private static object? GetValue(IReadOnlyDictionary<string, TySaveSetting> map, string name) =>
        map.TryGetValue(name, out var setting) ? setting.Value : null;

    private static void ApplyAchievementPatch(int achievementId, Dictionary<string, TySaveSetting> map, List<string> changed)
    {
        switch (achievementId)
        {
            case 1:
                SetUInt(map, changed, "RangsThrown", 1);
                break;
            case 2:
                SetInt(map, changed, "A4.1.M1", 1);
                break;
            case 3:
                SetInt(map, changed, "B4.1.M1", 1);
                break;
            case 4:
                SetInt(map, changed, "C4.2.M1", 1);
                break;
            case 7:
                SetInt(map, changed, "TurkeysCaught", 1);
                break;
            case 8:
                foreach (var name in new[] { "A1Complete", "A2Complete", "A3Complete", "B1Complete", "B2Complete", "B3Complete", "C1Complete", "C2Complete", "C3Complete", "A4.1.M1", "B4.1.M1", "C4.2.M1" })
                    SetInt(map, changed, name, 1);
                break;
            case 9:
                SetUInt(map, changed, "FrillsKilled", 100);
                break;
            case 10:
                SetUInt(map, changed, "TotalOpalsCollected", 50000);
                break;
            case 11:
                SetInt(map, changed, "DiveHard1", 1);
                SetInt(map, changed, "DiveHard2", 1);
                break;
            case 12:
                foreach (var name in new[] { "FrillsKilled", "BlueTonguesKilled", "DragonsKilled", "GeckosKilled", "GoannasKilled", "NanobotsKilled", "RoosKilled", "SkinksKilled", "RhinoBeetlesKilled", "SoldierCrabsKilled", "MagpiesKilled", "ReefSharksKilled", "CrocsKilled", "RobocrabsKilled" })
                    SetUInt(map, changed, name, 1);
                break;
            case 13:
                foreach (var name in new[] { "DamagetypeKillsEarth", "DamagetypeKillsFire", "DamagetypeKillsWater", "DamagetypeKillsAir", "DamagetypeKillsAges", "DamagetypeKillsDream", "DamagetypeKillsChaos", "DamagetypeKillsVoid", "DamagetypeKillsDeath" })
                    SetUInt(map, changed, name, 1);
                break;
            case 15:
                AddListItems(map, changed, "UnlockedRangs", "Hyperang", "Chaosrang", "Deadlyrang", "Cryptorang", "Disruptorang", "Doomerang");
                break;
            case 16:
                SetInt(map, changed, "DangerArena1", 1);
                SetInt(map, changed, "DangerArena2", 1);
                SetInt(map, changed, "DangerArena3", 1);
                break;
            case 17:
                SetFloat(map, changed, "DistanceGlided", 1001);
                break;
            case 18:
                SetFloat(map, changed, "DistanceRun", 10001);
                break;
            case 19:
                SetUInt(map, changed, "ChallengesWon", 50);
                break;
            case 20:
                SetInt(map, changed, "TimeAttackCompleted", 1);
                break;
        }
    }

    private static void SetInt(Dictionary<string, TySaveSetting> map, List<string> changed, string name, int minimum)
    {
        if (!map.TryGetValue(name, out var setting) || setting.Value is not int value || value >= minimum)
            return;

        map[name] = setting with { Value = minimum };
        changed.Add($"{name}: {value} -> {minimum}");
    }

    private static void SetUInt(Dictionary<string, TySaveSetting> map, List<string> changed, string name, uint minimum)
    {
        if (!map.TryGetValue(name, out var setting) || setting.Value is not uint value || value >= minimum)
            return;

        map[name] = setting with { Value = minimum };
        changed.Add($"{name}: {value} -> {minimum}");
    }

    private static void SetFloat(Dictionary<string, TySaveSetting> map, List<string> changed, string name, float minimum)
    {
        if (!map.TryGetValue(name, out var setting) || setting.Value is not float value || value >= minimum)
            return;

        map[name] = setting with { Value = minimum };
        changed.Add($"{name}: {value:0.###} -> {minimum:0.###}");
    }

    private static void AddListItems(Dictionary<string, TySaveSetting> map, List<string> changed, string name, params string[] items)
    {
        if (!map.TryGetValue(name, out var setting))
            return;

        var current = (setting.Value as IReadOnlyList<string>)?.ToList() ?? [];
        var added = new List<string>();
        foreach (var item in items)
        {
            if (current.Contains(item, StringComparer.OrdinalIgnoreCase))
                continue;

            current.Add(item);
            added.Add(item);
        }

        if (added.Count == 0)
            return;

        map[name] = setting with { Value = current };
        changed.Add($"{name}: added {string.Join(", ", added)}");
    }

    private static void WritePatchedSave(string savePath, ParsedTySave parsed, IReadOnlyList<string> changed)
    {
        var ordered = parsed.Settings
            .OrderBy(setting => setting.Index)
            .ToList();

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(HashSentinel);
            writer.Write(parsed.Version);
            writer.Write(ordered.Count);
            foreach (var setting in ordered)
            {
                writer.Write(setting.Hash);
                writer.Write(string.Empty);
                writer.Write((byte)((int)setting.Type | (setting.Temporary ? 0x80 : 0)));
                WriteSettingValue(writer, setting);
            }

            writer.Write(parsed.RemainderAfterSettings);
            var payloadLength = (int)stream.Position;
            var payload = stream.ToArray();
            var hash = Convert.ToBase64String(SHA512.HashData(payload));
            writer.Write(hash);
            stream.Position = 0;
            writer.Write(payloadLength);
        }

        File.WriteAllBytes(savePath, stream.ToArray());
    }

    private static void WriteSettingValue(BinaryWriter writer, TySaveSetting setting)
    {
        switch (setting.Type)
        {
            case TySettingType.Bool:
                writer.Write(setting.Value is bool b && b);
                break;
            case TySettingType.Float:
                writer.Write(Convert.ToSingle(setting.Value, CultureInfo.InvariantCulture));
                break;
            case TySettingType.Int:
                writer.Write(Convert.ToInt32(setting.Value, CultureInfo.InvariantCulture));
                break;
            case TySettingType.String:
                writer.Write(setting.Value?.ToString() ?? string.Empty);
                break;
            case TySettingType.List:
                var items = setting.Value as IReadOnlyList<string> ?? [];
                writer.Write(items.Count);
                foreach (var item in items)
                    writer.Write(item);
                break;
            case TySettingType.UInt:
                writer.Write(Convert.ToUInt32(setting.Value, CultureInfo.InvariantCulture));
                break;
            default:
                throw new InvalidDataException($"Unknown TY setting type '{setting.Type}'.");
        }
    }

    private sealed record ParsedTySave(
        string SavePath,
        byte[] Bytes,
        int PayloadLength,
        string StoredHash,
        int Version,
        List<TySaveSetting> Settings,
        byte[] RemainderAfterSettings);
}
