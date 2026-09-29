namespace AchievementLabs.Services.LegacyXbox;

public sealed record LegacyXboxTitleProfile(
    string Name,
    string Platform,
    string BundleId,
    string UrlScheme,
    string? ProductionClientOrTitleIdHex,
    string? ProductionTitleIdDecimal,
    string? PartnerClientOrTitleIdHex,
    string? PartnerTitleIdDecimal,
    string? ServiceConfigId,
    int AchievementPlatform,
    string ProgressEndpointTemplate,
    string UnlockPayloadTemplate);

public static class LegacyXboxTitleCatalog
{
    public const string LegacyXboxScope = "service::kdc.xboxlive.com::MBI_SSL";
    public const string ModernXboxScope = "XboxLive.signin XboxLive.offline_access";

    public static IReadOnlyList<LegacyXboxTitleProfile> KnownTitles { get; } =
    [
        new(
            "Halo: Spartan Assault iOS",
            "iOS",
            "com.microsoft.spartanassault",
            "spartanassault",
            "0000000048157A1B",
            "1209367067",
            "0000000068105080",
            "1745899648",
            "a48de20f-4009-4431-a4ad-d047e2fdff21",
            16,
            "https://achievements.xboxlive.com/users/xuid({xuid})/achievements/{achievementId}?titleId={titleId}",
            "{\"achievement\":{\"id\":\"{achievementId}\",\"platform\":{platform},\"titleId\":{titleId},\"unlocked\":true,\"unlockedOnline\":true,\"timeUnlocked\":\"{timestamp}\"}}"),

        new(
            "Halo: Spartan Strike iOS",
            "iOS",
            "com.microsoft.spartanstrike",
            "spartanstrike",
            "000000004415041A",
            "1142227994",
            "0000000068105081",
            "1745899649",
            "573018ea-f96b-469a-a198-9dd0394131ed",
            16,
            "https://achievements.xboxlive.com/users/xuid({xuid})/achievements/{achievementId}?titleId={titleId}",
            "{\"achievement\":{\"id\":\"{achievementId}\",\"platform\":{platform},\"titleId\":{titleId},\"unlocked\":true,\"unlockedOnline\":true,\"timeUnlocked\":\"{timestamp}\"}}"),

        new(
            "Halo: Spartan Assault Windows 8",
            "Windows 8",
            "com.microsoft.spartanassault",
            "spartanassault",
            null,
            "1297292157",
            null,
            null,
            "a48de20f-4009-4431-a4ad-d047e2fdff21",
            17,
            "https://progress.xboxlive.com/users/xuid({xuid})/progress/achievements/{achievementId}?titleId={titleId}",
            "{\"achievement\":{\"id\":{achievementId},\"platform\":{platform},\"titleId\":{titleId},\"unlocked\":true,\"unlockedOnline\":true,\"timeUnlocked\":\"{timestamp}\"}}"),

        new(
            "Halo: Spartan Strike Windows 8",
            "Windows 8",
            "com.microsoft.spartanstrike",
            "spartanstrike",
            null,
            "1297292194",
            null,
            null,
            "573018ea-f96b-469a-a198-9dd0394131ed",
            17,
            "https://progress.xboxlive.com/users/xuid({xuid})/progress/achievements/{achievementId}?titleId={titleId}",
            "{\"achievement\":{\"id\":{achievementId},\"platform\":{platform},\"titleId\":{titleId},\"unlocked\":true,\"unlockedOnline\":true,\"timeUnlocked\":\"{timestamp}\"}}"),

        new(
            "Fishdom 3: Special Edition Windows 8",
            "Windows 8",
            "Microsoft.Fishdom3SpecialEdition_8wekyb3d8bbwe",
            "fishdom",
            null,
            "1297292148",
            null,
            null,
            "2E389CF6-B86D-4369-95FC-7DE6E265B5C6",
            17,
            "https://progress.xboxlive.com/users/xuid({xuid})/progress/achievements/{achievementId}?titleId={titleId}",
            "{\"achievement\":{\"id\":{achievementId},\"platform\":{platform},\"titleId\":{titleId},\"unlocked\":true,\"unlockedOnline\":true,\"timeUnlocked\":\"{timestamp}\"}}"),

        new(
            "Cold Alley Windows 8",
            "Windows 8",
            "Microsoft.ColdAlley_8wekyb3d8bbwe",
            "coldalley",
            null,
            "1297292152",
            null,
            null,
            "923FC2CF-4999-4563-9585-321073578FD5",
            17,
            "https://progress.xboxlive.com/users/xuid({xuid})/progress/achievements/{achievementId}?titleId={titleId}",
            "{\"achievement\":{\"id\":{achievementId},\"platform\":{platform},\"titleId\":{titleId},\"unlocked\":true,\"unlockedOnline\":true,\"timeUnlocked\":\"{timestamp}\"}}"),

        new(
            "TY the Tasmanian Tiger Windows 8",
            "Windows 8",
            "Microsoft.TYtheTasmanianTiger_8wekyb3d8bbwe",
            "ty",
            null,
            "1297292136",
            null,
            null,
            "C9BA9EDA-875F-4432-BEE8-B06FB9573A61",
            17,
            "https://progress.xboxlive.com/users/xuid({xuid})/progress/achievements/{achievementId}?titleId={titleId}",
            "{\"achievement\":{\"id\":{achievementId},\"platform\":{platform},\"titleId\":{titleId},\"unlocked\":true,\"unlockedOnline\":true,\"timeUnlocked\":\"{timestamp}\"}}")
    ];
}
