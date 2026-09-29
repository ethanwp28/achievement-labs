namespace AchievementLabs.Models;

public sealed class Xbox360PackageInfo
{
    public string FilePath { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public string Magic { get; init; } = "Unknown";
    public string PackageType { get; init; } = "Unknown";
    public string ContentType { get; init; } = "Unknown";
    public string TitleId { get; init; } = "Unknown";
    public string MediaId { get; init; } = "Unknown";
    public string ProfileId { get; init; } = "Unknown";
    public string DeviceId { get; init; } = "Unknown";
    public string DisplayName { get; init; } = "Unknown";
    public string TitleName { get; init; } = "Unknown";
    public bool LooksLikeStfsPackage { get; init; }
    public IReadOnlyList<Xbox360PackageProbe> Probes { get; init; } = [];
    public IReadOnlyList<Xbox360PackageEntry> Entries { get; init; } = [];
}

public sealed class Xbox360PackageProbe
{
    public string Name { get; init; } = string.Empty;
    public string Offset { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}

public sealed class Xbox360PackageEntry
{
    public int Index { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public bool IsDirectory { get; init; }
    public bool IsConsecutive { get; init; }
    public int Blocks { get; init; }
    public int StartBlock { get; init; }
    public int PathIndicator { get; init; }
    public long Size { get; init; }
}
