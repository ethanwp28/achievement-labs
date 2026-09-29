namespace AchievementLabs.Services.LegacyXbox;

public sealed record LegacyProgressUnlockResult(
    int StatusCode,
    string ReasonPhrase,
    string RequestUri,
    string RequestBody,
    string ResponseBody,
    bool IsSuccess);
