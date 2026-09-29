using System.Text;
namespace AchievementLabs.Desktop;
public sealed partial class DesktopModel
{
    private string platformFilter = "All", titleLookup = "";
    public string[] PlatformFilters { get; } = ["All", "Xbox One/Series", "PC", "Xbox 360", "Win32", "Windows 8/Legacy", "Incomplete Games"];
    public string PlatformFilter { get => platformFilter; set { platformFilter = value ?? "All"; Changed(); Changed(nameof(VisibleGames)); } }
    public string TitleLookup { get => titleLookup; set { titleLookup = value ?? ""; Changed(); } }
    public bool CanQuery => !QueueActive && !busy && session != null;
    public bool CanExport => achievements.Length > 0;
    public static bool HasDevice(Game game, string device) => game.Platform.Split('/', StringSplitOptions.TrimEntries).Contains(device, StringComparer.OrdinalIgnoreCase);
    public static bool UsesLegacyEndpoint(Game game) => new[] { "Xbox360", "Mobile", "WindowsPhone", "Win8", "Windows8" }.Any(d => HasDevice(game, d));
    public static bool MatchesPlatform(Game game, string filter) => filter switch
    {
        "Xbox One/Series" => HasDevice(game, "XboxOne") || HasDevice(game, "XboxSeries"),
        "PC" => HasDevice(game, "PC"), "Xbox 360" => HasDevice(game, "Xbox360"), "Win32" => HasDevice(game, "Win32"),
        "Windows 8/Legacy" => UsesLegacyEndpoint(game), "Incomplete Games" => game.ProgressKnown && game.Completed < game.Total, _ => true
    };
    public async Task LookupTitleAsync()
    {
        if (!CanQuery) return;
        if (!uint.TryParse(TitleLookup.Trim(), out var id) || id == 0) { Notice = "Enter a numeric Xbox title ID."; return; }
        var key = id.ToString();
        await SelectGameAsync(Games.FirstOrDefault(g => g.Id == key) ?? new Game(key, "Title " + key, "XboxOne", 0, 0, 0, false));
    }
    public async Task RefreshAchievementsAsync()
    {
        if (CanQuery && SelectedGame is { } game) await SelectGameAsync(game);
    }
    public async Task RefreshLibraryAsync()
    {
        if (!CanQuery) return;
        Busy(true);
        try
        {
            await requests.WaitAsync(lifetime.Token);
            try
            {
                if (client == null || session == null) return;
                var api = client; var xuid = session.Xuid;
                var result = await Task.Run(() => api.GetGamesListAsync(xuid), lifetime.Token) ?? throw new InvalidDataException();
                lifetime.Token.ThrowIfCancellationRequested();
                Games = result.Titles.Where(t => t.TitleId != null).Select(t => new Game(t.TitleId!, t.Name ?? t.TitleId!, string.Join(" / ", t.Devices), t.Achievement?.CurrentAchievements ?? 0, t.Achievement?.TotalAchievements ?? 0, t.Achievement?.CurrentGamerscore ?? 0, t.Achievement != null, ResolveTitleImage(t.DisplayImage, t.Images))).OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToArray();
                Notice = $"Refreshed {Games.Length} titles.";
            }
            finally { requests.Release(); }
        }
        catch (OperationCanceledException) { }
        catch { Notice = "Could not refresh the library. Your previous list is still available."; }
        finally { Busy(false); }
    }
    public async Task ExportCsvAsync(string path)
    {
        static string Quote(string text) => "\"" + text.Replace("\"", "\"\"") + "\"";
        var rows = new List<string> { "Title ID,Title,Achievement ID,Achievement,Description,Status,Gamerscore" };
        foreach (var a in VisibleAchievements)
            rows.Add(string.Join(",", new[] { SelectedGame?.Id ?? "", SelectedGame?.Name ?? "", a.Id, a.Name, a.Description, a.Status, a.ScoreKnown ? a.Score.ToString() : "" }.Select(Quote)));
        await File.WriteAllLinesAsync(path, rows, new UTF8Encoding(true), lifetime.Token);
        Notice = $"Exported {rows.Count - 1} visible achievements.";
    }
}
