namespace AchievementLabs.Services.LegacyXbox.Ty;

public enum TySettingType
{
    Bool = 0,
    Float = 1,
    Int = 2,
    String = 3,
    List = 4,
    UInt = 5
}

public sealed record TySaveSetting(
    int Index,
    int Hash,
    string Name,
    TySettingType Type,
    bool Temporary,
    object? Value);
