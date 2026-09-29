using Memory;

namespace AchievementLabs.Core;

public static class XboxPcAppAuthorizationReader
{
    private const string ProcessName = "XboxPcApp";
    private const string XauthPattern = "58 42 4C 33 2E 30 20 78 3D";

    public static async Task<string?> TryReadAsync(CancellationToken cancellationToken = default)
    {
        var memory = new Mem();
        if (!memory.OpenProcess(ProcessName)) return null;

        var addresses = (await memory.AoBScan(XauthPattern, true)).ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        if (addresses.Length == 0) return null;

        var candidates = addresses
            .Select(address => memory.ReadString(address.ToString("X"), length: 10000))
            .Where(value => value.StartsWith("XBL3.0 x=", StringComparison.Ordinal))
            .ToArray();
        if (candidates.Length == 0) return null;

        var mostCommon = candidates
            .GroupBy(value => value, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .First();

        // Match XAU 2.8.1's guard against selecting a stray token-shaped string.
        return mostCommon.Count() > 3 ? mostCommon.Key : null;
    }
}
