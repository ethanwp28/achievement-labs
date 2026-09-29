namespace AchievementLabs.Services.LegacyXbox.Win8;

public sealed record Win8SaveFileInfo(
    string Path,
    long Length,
    DateTime LastWriteTime,
    string Sha256);
