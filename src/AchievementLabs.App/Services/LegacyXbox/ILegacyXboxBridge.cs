namespace AchievementLabs.Services.LegacyXbox;

public interface ILegacyXboxBridge
{
    Task<LegacyAuthBridgeResult> ConvertMsaAccessTokenAsync(string msaAccessToken, string relyingParty = "http://xboxlive.com");

    Task<LegacySignedAuthBridgeResult> ConvertMsaAccessTokenToSignedWin8Async(
        string msaAccessToken,
        bool allowWin32Fallback = false,
        string relyingParty = "http://xboxlive.com");

    Task<LegacySignedAuthBridgeResult> ConvertMsaAccessTokenToSignedWin32Async(
        string msaAccessToken,
        string relyingParty = "http://xboxlive.com");

    Task<IReadOnlyList<LegacyDeviceAuthProbeResult>> ProbeSignedDeviceAuthAsync();

    Task<LegacyProgressUnlockResult> SendProgressUnlockAsync(
        LegacyXboxTitleProfile profile,
        string authorizationHeader,
        string xuid,
        string titleId,
        string achievementId,
        int achievementPlatform,
        DateTime unlockTimeUtc,
        LegacyUnlockRequestMode requestMode,
        CancellationToken cancellationToken = default);

    Task<LegacyAchievementReadResult> ReadAchievementsAsync(
        string authorizationHeader,
        string xuid,
        string titleId,
        LegacyAchievementReadSource source,
        CancellationToken cancellationToken = default);
}
