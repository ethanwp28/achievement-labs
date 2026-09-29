namespace AchievementLabs.Services.LegacyXbox;

public sealed record LegacyDeviceAuthProbeResult(
    string DeviceType,
    bool Success,
    string Details);
