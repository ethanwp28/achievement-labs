namespace AchievementLabs.Services.LegacyXbox;

public sealed record LegacySignedAuthBridgeResult(
    string AuthorizationHeader,
    string UserHash,
    string Xuid,
    string XstsToken,
    string? ExpiresOn,
    string DeviceType,
    string DeviceVersion,
    bool IncludedSerialNumber,
    string TicketFormat);
