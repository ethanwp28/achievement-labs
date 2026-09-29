namespace AchievementLabs.Services.LegacyXbox;

public enum LegacyAchievementReadSource
{
    Profile,
    Progress
}

public sealed record LegacyAchievementReadResult(
    int StatusCode,
    string ReasonPhrase,
    string RequestUri,
    string ResponseBody,
    bool IsSuccess);
