namespace AchievementLabs.Services.LegacyXbox;

public sealed record LegacyAuthBridgeResult(
    string AuthorizationHeader,
    string UserHash,
    string Xuid,
    string XstsToken,
    string? ExpiresOn);
