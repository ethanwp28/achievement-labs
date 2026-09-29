using XboxAuthNet.XboxLive;

namespace AchievementLabs.Services.Auth;

public sealed record XboxOAuthClientProfile(
    string Name,
    string ClientId,
    string DeviceType,
    string DeviceVersion)
{
    public static XboxOAuthClientProfile XboxAppPc { get; } = new(
        "Xbox App PC",
        XboxGameTitles.XboxAppPC,
        XboxDeviceTypes.Win32,
        "0.0.0");

    public static IReadOnlyList<XboxOAuthClientProfile> KnownProfiles { get; } =
    [
        XboxAppPc,
        new("Xbox App iOS", XboxGameTitles.XboxAppIOS, XboxDeviceTypes.iOS, "0.0.0"),
        new("Xbox Game Pass iOS", XboxGameTitles.XboxGamepassIOS, XboxDeviceTypes.iOS, "0.0.0"),
        new("Minecraft Android", XboxGameTitles.MinecraftAndroid, XboxDeviceTypes.Android, "0.0.0"),
        new("Minecraft Nintendo Switch", XboxGameTitles.MinecraftNintendoSwitch, XboxDeviceTypes.Nintendo, "0.0.0"),
        new("Minecraft Java", XboxGameTitles.MinecraftJava, XboxDeviceTypes.Win32, "0.0.0")
    ];
}
