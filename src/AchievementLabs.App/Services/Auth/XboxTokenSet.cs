using XboxAuthNet.OAuth;

namespace AchievementLabs.Services.Auth;

public sealed record XboxTokenSet(
    string XAuth,
    string EventsToken,
    MicrosoftOAuthResponse OAuthResponse,
    XboxOAuthClientProfile ClientProfile);
