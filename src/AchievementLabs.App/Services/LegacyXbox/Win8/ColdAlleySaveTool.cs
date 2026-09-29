using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace AchievementLabs.Services.LegacyXbox.Win8;

public sealed class ColdAlleySaveTool : IWin8SaveTool
{
    private const string PackageFamilyName = "Microsoft.ColdAlley_8wekyb3d8bbwe";
    private static readonly string[] SaveNames = ["2535410798709357_sr.bin", "profile_copy.bin", "tts_settings.bin", "tts_add.bin"];
    private const int AchievementFlagBaseOffset = 0x0818;
    private const int PersistentProgressBaseOffset = 0x0418;
    private const int CareerKillsProgressIndex = 0x0E;

    private static readonly ColdAlleyPlainPatch[] ButtonsSchoolSupportingPatches =
    [
        new(0, 0x041C, [0x06]),
        new(0, 0x0424, [0x06]),
        new(0, 0x0500, [0x01])
    ];

    private static readonly ColdAlleyAchievementDefinition[] Definitions =
    [
        CreateCounterDefinition(1, "10 Kills", "Neutralize 10 enemies throughout your career", CareerKillsProgressIndex, 10),
        CreateCounterDefinition(2, "25 Kills", "Neutralize 25 enemies throughout your career", CareerKillsProgressIndex, 25),
        CreateCounterDefinition(3, "50 Kills", "Neutralize 50 enemies throughout your career", CareerKillsProgressIndex, 50),
        CreateCounterDefinition(4, "100 Kills", "Neutralize 100 enemies throughout your career", CareerKillsProgressIndex, 100),
        CreateCounterDefinition(5, "200 Kills", "Neutralize 200 enemies throughout your career", CareerKillsProgressIndex, 200),
        CreateCounterDefinition(6, "300 Kills", "Neutralize 300 enemies throughout your career", CareerKillsProgressIndex, 300),
        CreateCounterDefinition(7, "500 Kills", "Neutralize 500 enemies throughout your career", CareerKillsProgressIndex, 500),
        CreateCounterDefinition(8, "1000 Kills", "Neutralize 1000 enemies throughout your career", CareerKillsProgressIndex, 1000),
        CreateCounterDefinition(9, "5000 Kills", "Neutralize 5000 enemies throughout your career", CareerKillsProgressIndex, 5000),
        CreateCounterDefinition(10, "10000 Kills", "Neutralize 10000 enemies throughout your career", CareerKillsProgressIndex, 10000),
        CreateCounterDefinition(11, "Buttons School", "Complete the HUD and Interface tutorial", 0x3A, 1, ButtonsSchoolSupportingPatches),
        CreateCounterDefinition(12, "Flying School", "Complete the arcade controls tutorial", 0x3B, 1),
        CreateCounterDefinition(13, "Piloting School", "Complete the simulation controls tutorial", 0x3C, 1),
        CreateCounterDefinition(14, "Takeoff School", "Complete the Takeoff and Landing tutorial", 0x3D, 1),
        CreateCounterDefinition(15, "Weapons School", "Complete the weapons tutorial", 0x3E, 1),
        CreateCounterDefinition(16, "Flag Capture", "Capture the enemy Flag in a CTF match", 0x19, 1),
        CreateCounterDefinition(17, "Flag Return", "Return your team's flag to your base in a CTF match", 0x1A, 1),
        CreateCounterDefinition(18, "Enemy Base Destroy", "Destroy the enemy base in a DTB match", 0x17, 1)
    ];

    public string GameKey => "coldalley";
    public string DisplayName => "Cold Alley";
    public bool CanPrepare => true;
    public int MaxAchievementId => 18;

    public string GetPrimarySavePath(long xuid)
    {
        return Path.Combine(GetLocalStatePath(), $"{xuid}_sr.bin");
    }

    public Win8SaveSnapshot Read(long xuid)
    {
        var primary = GetPrimarySavePath(xuid);
        var files = CollectFiles(xuid);
        if (files.Count == 0)
            return Win8SaveSnapshot.Missing(DisplayName, primary, xuid, "Cold Alley binary profile files were not found. Launch Cold Alley once with this Xbox profile, then refresh.");

        var sr = files.FirstOrDefault(file => file.Path.EndsWith("_sr.bin", StringComparison.OrdinalIgnoreCase));
        var profileCopy = files.FirstOrDefault(file => file.Path.EndsWith("profile_copy.bin", StringComparison.OrdinalIgnoreCase));
        var tts = files.FirstOrDefault(file => file.Path.EndsWith("tts_settings.bin", StringComparison.OrdinalIgnoreCase));
        var blockSummary = sr is not null ? BuildBlockSummary(sr.Path) : "Primary profile missing.";
        var summary = sr is not null && profileCopy is not null && sr.Sha256 == profileCopy.Sha256
            ? $"Primary profile and profile_copy match. Verified packed-block codec active. {blockSummary}"
            : $"Cold Alley profile files found. Verified packed-block codec active. {blockSummary}";

        var diagnostics = Definitions
            .Select(definition => new Win8AchievementDiagnostic(
                definition.Id,
                definition.Name,
                IsLikelyPrepared(files, definition),
                BuildEvidence(files, definition)))
            .ToList();

        if (tts is not null)
            summary += $" Settings profile hash: {tts.Sha256[..12]}.";

        return new Win8SaveSnapshot(DisplayName, primary, xuid, File.Exists(primary), "Binary profile", summary, diagnostics, files);
    }

    public string Backup(long xuid)
    {
        var files = CollectFiles(xuid);
        if (files.Count == 0)
            throw new FileNotFoundException("No Cold Alley save/profile files were found.");

        var backupDir = Path.Combine(GetLocalStatePath(), $"vega-backup-{DateTime.Now:yyyyMMdd-HHmmss}");
        Directory.CreateDirectory(backupDir);
        foreach (var file in files)
            File.Copy(file.Path, Path.Combine(backupDir, Path.GetFileName(file.Path)), overwrite: false);

        return backupDir;
    }

    public Win8SavePatchResult PrepareAchievement(long xuid, int achievementId)
    {
        var definition = Definitions.FirstOrDefault(item => item.Id == achievementId)
            ?? throw new ArgumentOutOfRangeException(nameof(achievementId), "Unknown Cold Alley achievement ID.");

        return Patch(xuid, [definition]);
    }

    public Win8SavePatchResult PrepareAll(long xuid)
    {
        return Patch(xuid, Definitions);
    }

    private static Win8SavePatchResult Patch(long xuid, IReadOnlyList<ColdAlleyAchievementDefinition> definitions)
    {
        var files = CollectFiles(xuid);
        if (files.Count == 0)
            throw new FileNotFoundException("No Cold Alley save/profile files were found.");

        var primary = GetPrimarySavePathStatic(xuid);
        var backupPath = BackupFiles(files);
        var changed = new List<string>();
        var verifiedDefinitions = definitions.Where(definition => definition.IsVerified).ToList();
        var skippedDefinitions = definitions.Where(definition => !definition.IsVerified).ToList();

        foreach (var definition in skippedDefinitions)
            changed.Add($"{definition.Name}: skipped; Cold Alley loader flag is not mapped yet.");

        if (verifiedDefinitions.Count == 0)
        {
            changed.Add("No verified Cold Alley save patches were selected.");
            return new Win8SavePatchResult(
                primary,
                backupPath,
                definitions.Select(definition => definition.Id).ToList(),
                changed);
        }

        var wroteAnyFile = false;
        foreach (var file in files.Where(file => IsProfileSave(file.Path)))
        {
            var bytes = File.ReadAllBytes(file.Path);
            var fileChanges = ApplyPackedPatches(bytes, file.Path, verifiedDefinitions);
            var hasByteChanges = fileChanges.Any(change => change.Contains("->", StringComparison.Ordinal));

            if (hasByteChanges)
            {
                File.WriteAllBytes(file.Path, bytes);
                wroteAnyFile = true;
            }

            changed.AddRange(fileChanges);
        }

        if (wroteAnyFile)
            SyncPrimaryProfileCopy(xuid, changed);

        if (changed.Count == 0)
            changed.Add("Selected Cold Alley achievements were already prepared.");

        return new Win8SavePatchResult(
            primary,
            backupPath,
            definitions.Select(definition => definition.Id).ToList(),
            changed);
    }

    private static bool IsLikelyPrepared(IReadOnlyList<Win8SaveFileInfo> files, ColdAlleyAchievementDefinition definition)
    {
        var primary = files.FirstOrDefault(file => file.Path.EndsWith("_sr.bin", StringComparison.OrdinalIgnoreCase));
        if (primary is null || !definition.IsVerified)
            return false;

        var bytes = File.ReadAllBytes(primary.Path);
        var blocks = ColdAlleySaveCodec.FindPackedBlocks(bytes);
        return definition.Patches.All(patch => patch.IsApplied(blocks));
    }

    private static string BuildEvidence(IReadOnlyList<Win8SaveFileInfo> files, ColdAlleyAchievementDefinition definition)
    {
        var primary = files.FirstOrDefault(file => file.Path.EndsWith("_sr.bin", StringComparison.OrdinalIgnoreCase));
        if (primary is null)
            return $"{definition.Trigger}. Primary profile missing.";

        if (!definition.IsVerified)
            return $"{definition.Trigger}. Loader flag not mapped yet; capture/static analysis still needed.";

        var bytes = File.ReadAllBytes(primary.Path);
        var blocks = ColdAlleySaveCodec.FindPackedBlocks(bytes);
        var values = definition.Patches
            .Select(patch => patch.Describe(blocks))
            .ToList();

        return $"{definition.Trigger}. Verified plaintext fields: {string.Join(", ", values)}.";
    }

    private static string GetLocalStatePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages",
            PackageFamilyName,
            "LocalState");
    }

    private static string GetPrimarySavePathStatic(long xuid)
    {
        return Path.Combine(GetLocalStatePath(), $"{xuid}_sr.bin");
    }

    private static IReadOnlyList<Win8SaveFileInfo> CollectFiles(long xuid)
    {
        var localState = GetLocalStatePath();
        var names = SaveNames
            .Select(name => name.Replace("2535410798709357", xuid.ToString(), StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return names
            .Select(name => Path.Combine(localState, name))
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

    private static ColdAlleyAchievementDefinition CreateFlagDefinition(
        int id,
        string name,
        string trigger,
        IReadOnlyList<ColdAlleyPlainPatch>? supportingPatches = null)
    {
        var patches = new List<ColdAlleyPlainPatch>
        {
            new(0, AchievementFlagBaseOffset + id, [0x01])
        };

        if (supportingPatches is not null)
            patches.AddRange(supportingPatches);

        return new ColdAlleyAchievementDefinition(id, name, trigger, true, patches);
    }

    private static ColdAlleyAchievementDefinition CreateCounterDefinition(
        int id,
        string name,
        string trigger,
        int progressIndex,
        uint threshold,
        IReadOnlyList<ColdAlleyPlainPatch>? supportingPatches = null)
    {
        var patches = new List<ColdAlleyPlainPatch>
        {
            ColdAlleyPlainPatch.AtLeastUInt32(0, PersistentProgressBaseOffset + progressIndex * sizeof(uint), threshold),
            new(0, AchievementFlagBaseOffset + id, [0x01])
        };

        if (supportingPatches is not null)
            patches.AddRange(supportingPatches);

        return new ColdAlleyAchievementDefinition(id, name, $"{trigger}; event counter 0x{progressIndex:X} >= {threshold}", true, patches);
    }

    private static ColdAlleyAchievementDefinition CreateUnmappedDefinition(int id, string name, string trigger) =>
        new(id, name, trigger, false, []);

    private static string BackupFiles(IReadOnlyList<Win8SaveFileInfo> files)
    {
        var backupDir = Path.Combine(GetLocalStatePath(), $"vega-backup-{DateTime.Now:yyyyMMdd-HHmmss}");
        Directory.CreateDirectory(backupDir);
        foreach (var file in files)
            File.Copy(file.Path, Path.Combine(backupDir, Path.GetFileName(file.Path)), overwrite: false);

        return backupDir;
    }

    private static bool IsProfileSave(string path)
    {
        var name = Path.GetFileName(path);
        return name.EndsWith("_sr.bin", StringComparison.OrdinalIgnoreCase)
            || name.Equals("profile_copy.bin", StringComparison.OrdinalIgnoreCase)
            || name.Equals("tts_settings.bin", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildBlockSummary(string path)
    {
        try
        {
            var blocks = ColdAlleySaveCodec.FindPackedBlocks(File.ReadAllBytes(path));
            return blocks.Count == 0
                ? "No valid encrypted profile blocks were found."
                : $"{blocks.Count} encrypted profile blocks validated.";
        }
        catch (Exception ex)
        {
            return $"Encrypted profile block scan failed: {ex.Message}";
        }
    }

    private static List<string> ApplyPackedPatches(byte[] bytes, string path, IReadOnlyList<ColdAlleyAchievementDefinition> definitions)
    {
        var changed = new List<string>();
        var blocks = ColdAlleySaveCodec.FindPackedBlocks(bytes);
        if (blocks.Count == 0)
        {
            changed.Add($"{Path.GetFileName(path)}: no valid encrypted profile blocks found.");
            return changed;
        }

        foreach (var definition in definitions)
        {
            var definitionChanged = false;
            foreach (var patch in definition.Patches)
            {
                if (patch.Apply(blocks, out var detail))
                {
                    changed.Add($"{Path.GetFileName(path)} {definition.Name}: {detail}");
                    definitionChanged = true;
                }
            }

            if (!definitionChanged)
                changed.Add($"{Path.GetFileName(path)} {definition.Name}: already prepared.");
        }

        foreach (var block in blocks.Where(block => block.IsDirty))
            block.WriteBack(bytes);

        return changed;
    }

    private static void SyncPrimaryProfileCopy(long xuid, List<string> changed)
    {
        var primary = GetPrimarySavePathStatic(xuid);
        var profileCopy = Path.Combine(GetLocalStatePath(), "profile_copy.bin");
        if (!File.Exists(primary) || !File.Exists(profileCopy))
            return;

        File.Copy(primary, profileCopy, overwrite: true);
        changed.Add("profile_copy.bin synced from primary XUID profile after patch.");
    }

    private sealed record ColdAlleyAchievementDefinition(
        int Id,
        string Name,
        string Trigger,
        bool IsVerified,
        IReadOnlyList<ColdAlleyPlainPatch> Patches);

    private sealed record ColdAlleyPlainPatch(int BlockIndex, int PlainOffset, byte[] Expected, bool IsMinimumUInt32 = false)
    {
        public static ColdAlleyPlainPatch AtLeastUInt32(int blockIndex, int plainOffset, uint minimum) =>
            new(blockIndex, plainOffset, BitConverter.GetBytes(minimum), true);

        public bool IsApplied(IReadOnlyList<ColdAlleyPackedBlock> blocks)
        {
            if (BlockIndex < 0 || BlockIndex >= blocks.Count)
                return false;

            var block = blocks[BlockIndex];
            if (PlainOffset < 0 || PlainOffset + Expected.Length > block.Plaintext.Length)
                return false;

            if (IsMinimumUInt32)
                return BitConverter.ToUInt32(block.Plaintext, PlainOffset) >= BitConverter.ToUInt32(Expected);

            return block.Plaintext.AsSpan(PlainOffset, Expected.Length).SequenceEqual(Expected);
        }

        public string Describe(IReadOnlyList<ColdAlleyPackedBlock> blocks)
        {
            if (BlockIndex < 0 || BlockIndex >= blocks.Count)
                return $"block {BlockIndex + 1} +0x{PlainOffset:X}=<missing>";

            var block = blocks[BlockIndex];
            if (PlainOffset < 0 || PlainOffset + Expected.Length > block.Plaintext.Length)
                return $"block {BlockIndex + 1} +0x{PlainOffset:X}=<out of range>";

            if (IsMinimumUInt32)
            {
                var current = BitConverter.ToUInt32(block.Plaintext, PlainOffset);
                var expected = BitConverter.ToUInt32(Expected);
                return $"block {BlockIndex + 1} +0x{PlainOffset:X}={current} (needs >= {expected})";
            }

            return $"block {BlockIndex + 1} +0x{PlainOffset:X}={Convert.ToHexString(block.Plaintext.AsSpan(PlainOffset, Expected.Length))}";
        }

        public bool Apply(IReadOnlyList<ColdAlleyPackedBlock> blocks, out string detail)
        {
            detail = string.Empty;
            if (BlockIndex < 0 || BlockIndex >= blocks.Count)
            {
                detail = $"block {BlockIndex + 1} missing";
                return false;
            }

            var block = blocks[BlockIndex];
            if (PlainOffset < 0 || PlainOffset + Expected.Length > block.Plaintext.Length)
            {
                detail = $"block {BlockIndex + 1} +0x{PlainOffset:X} out of range";
                return false;
            }

            var before = block.Plaintext.AsSpan(PlainOffset, Expected.Length).ToArray();
            if (IsMinimumUInt32)
            {
                var current = BitConverter.ToUInt32(before);
                var minimum = BitConverter.ToUInt32(Expected);
                if (current >= minimum)
                    return false;

                Expected.CopyTo(block.Plaintext, PlainOffset);
                block.IsDirty = true;
                detail = $"block {BlockIndex + 1} +0x{PlainOffset:X}: {current} -> {minimum}";
                return true;
            }

            if (before.SequenceEqual(Expected))
                return false;

            Expected.CopyTo(block.Plaintext, PlainOffset);
            block.IsDirty = true;
            detail = $"block {BlockIndex + 1} +0x{PlainOffset:X}: {Convert.ToHexString(before)} -> {Convert.ToHexString(Expected)}";
            return true;
        }
    }

    private sealed class ColdAlleyPackedBlock(int offset, int length, byte[] plaintext)
    {
        public int Offset { get; } = offset;
        public int Length { get; } = length;
        public byte[] Plaintext { get; } = plaintext;
        public bool IsDirty { get; set; }

        public void WriteBack(byte[] saveBytes)
        {
            var finalized = ColdAlleySaveCodec.UpdateChecksum(Plaintext);
            var encrypted = ColdAlleySaveCodec.TransformBlock(finalized, decrypt: false);
            encrypted.CopyTo(saveBytes, Offset + sizeof(int));
        }
    }

    private static class ColdAlleySaveCodec
    {
        private const uint Mask32 = uint.MaxValue;
        private const uint Delta = 0x9E3669B9;
        private const uint DecryptSum = 0xC6CD3720;
        private static readonly byte[] KeyBytes = "afe770e719c4797b43750edfafbf199d4bbab5d5"u8.ToArray();
        private static readonly uint[] SeedKey =
        [
            0x35A20053,
            0x39531FBF,
            0x1D60B43F,
            0x13EBB543,
            0x3817CD4D,
            0x0AB1D2ED,
            0x18A04C6F,
            0x08716C13,
            0x106D643D,
            0x296A882D
        ];

        public static List<ColdAlleyPackedBlock> FindPackedBlocks(byte[] saveBytes)
        {
            var blocks = new List<ColdAlleyPackedBlock>();
            for (var offset = 0; offset <= saveBytes.Length - 12; offset++)
            {
                var length = ReadUInt32(saveBytes, offset);
                if (length == 0 || length > 0x1000 || length % 8 != 0)
                    continue;

                var end = offset + sizeof(int) + (int)length;
                if (end > saveBytes.Length)
                    continue;

                var encrypted = saveBytes.AsSpan(offset + sizeof(int), (int)length).ToArray();
                var plaintext = TransformBlock(encrypted, decrypt: true);
                if (ChecksumOk(plaintext))
                    blocks.Add(new ColdAlleyPackedBlock(offset, (int)length, plaintext));
            }

            return blocks;
        }

        public static byte[] TransformBlock(byte[] payload, bool decrypt)
        {
            if (payload.Length % 8 != 0)
                throw new InvalidDataException("Cold Alley packed block length must be 8-byte aligned.");

            var data = payload.ToArray();
            var rollingKey = SeedKey.ToArray();
            for (var blockIndex = 0; blockIndex < data.Length / 8; blockIndex++)
            {
                var replaceIndex = (blockIndex & 1) == 1 ? blockIndex % 10 : blockIndex & 7;
                rollingKey[replaceIndex] = KeyDword(blockIndex);
                var keyStart = blockIndex % 7;
                Span<uint> roundKey = stackalloc uint[4];
                for (var index = 0; index < 4; index++)
                    roundKey[index] = rollingKey[keyStart + index];

                var offset = blockIndex * 8;
                var v0 = ReadUInt32(data, offset);
                var v1 = ReadUInt32(data, offset + sizeof(uint));
                (v0, v1) = decrypt
                    ? DecryptPair(v0, v1, roundKey)
                    : EncryptPair(v0, v1, roundKey);
                WriteUInt32(data, offset, v0);
                WriteUInt32(data, offset + sizeof(uint), v1);
            }

            return data;
        }

        public static byte[] UpdateChecksum(byte[] plaintext)
        {
            var data = plaintext.ToArray();
            uint checksum = 1;
            for (var offset = 4; offset < data.Length; offset += 4)
                checksum = unchecked(checksum + 1 + ReadUInt32(data, offset));

            WriteUInt32(data, 0, checksum);
            return data;
        }

        private static bool ChecksumOk(byte[] plaintext)
        {
            var checksum = ReadUInt32(plaintext, 0);
            for (var offset = 4; offset < plaintext.Length; offset += 4)
                checksum = unchecked(checksum - 1 - ReadUInt32(plaintext, offset));

            return checksum == 1;
        }

        private static uint KeyDword(int blockIndex)
        {
            var offset = blockIndex % 0x24;
            Span<byte> value = stackalloc byte[4];
            for (var index = 0; index < 4; index++)
            {
                var keyIndex = offset + index;
                value[index] = keyIndex < KeyBytes.Length ? KeyBytes[keyIndex] : (byte)0;
            }

            return BitConverter.ToUInt32(value);
        }

        private static (uint V0, uint V1) EncryptPair(uint v0, uint v1, ReadOnlySpan<uint> key)
        {
            uint sum = 0;
            for (var round = 0; round < 32; round++)
            {
                sum = unchecked(sum + Delta);
                var mix = unchecked(((v1 >> 5) + key[1]) ^ ((v1 << 4) + key[0]) ^ (sum + v1));
                v0 = unchecked(v0 + mix);
                mix = unchecked(((v0 >> 5) + key[3]) ^ ((v0 << 4) + key[2]) ^ (sum + v0));
                v1 = unchecked(v1 + mix);
            }

            return (v0, v1);
        }

        private static (uint V0, uint V1) DecryptPair(uint v0, uint v1, ReadOnlySpan<uint> key)
        {
            uint sum = DecryptSum;
            for (var round = 0; round < 32; round++)
            {
                var mix = unchecked(((v0 >> 5) + key[3]) ^ ((v0 << 4) + key[2]) ^ (sum + v0));
                v1 = unchecked(v1 - mix);
                mix = unchecked(((v1 >> 5) + key[1]) ^ ((v1 << 4) + key[0]) ^ (sum + v1));
                v0 = unchecked(v0 - mix);
                sum = unchecked(sum + 0x61C99647);
            }

            return (v0, v1);
        }

        private static uint ReadUInt32(byte[] bytes, int offset)
        {
            return BitConverter.ToUInt32(bytes, offset);
        }

        private static void WriteUInt32(byte[] bytes, int offset, uint value)
        {
            BitConverter.GetBytes(value).CopyTo(bytes, offset);
        }
    }
}
