using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Text;
namespace AchievementLabs.Desktop;
public sealed record TitleSearchResult(string TitleId, string Name, string Platform, string? ImageUrl)
{
    public string Monogram => string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(word => char.ToUpperInvariant(word[0])));
}
public sealed partial class DesktopModel
{
    private string titleSearch = "", gamertagSearch = "", lookupOutput = "", lookupXuid = "";
    private TitleSearchResult[] titleSearchResults = [];
    private bool excludeZeroScore, exclude360;
    private string converterStatus = "Not scanned.";
    public bool IsTools => page == "Tools";
    public bool IsAbout => page == "About";
    public string TitleSearch { get => titleSearch; set { titleSearch = value; Changed(); } }
    public string GamertagSearch { get => gamertagSearch; set { gamertagSearch = value; Changed(); } }
    public string LookupOutput { get => lookupOutput; private set { lookupOutput = value; Changed(); } }
    public TitleSearchResult[] TitleSearchResults { get => titleSearchResults; private set { titleSearchResults = value; Changed(); } }
    public bool ExcludeZeroScore { get => excludeZeroScore; set { excludeZeroScore = value; Changed(); } }
    public bool Exclude360 { get => exclude360; set { exclude360 = value; Changed(); } }
    public string ConverterStatus { get => converterStatus; private set { converterStatus = value; Changed(); } }
    public void RefreshConverterStatus()
    {
        var path = steam.GetConverterPath();
        ConverterStatus = !string.IsNullOrWhiteSpace(path) && Directory.Exists(path)
            ? "Converter workspace found: " + path
            : "Converter workspace was not found. The original converter workflow is still a folder-only beta.";
    }
    public async Task SearchTitlesAsync()
    {
        if (busy || string.IsNullOrWhiteSpace(TitleSearch)) return; Busy(true);
        try
        {
            using var http = new HttpClient();
            var json = JObject.Parse(await http.GetStringAsync("https://dbox.tools/api/title_ids/?name=" + Uri.EscapeDataString(TitleSearch) + "&limit=100&offset=0", lifetime.Token));
            TitleSearchResults = (json["items"] as JArray ?? [])
                .Select(item =>
                {
                    var hexadecimalId = item["title_id"]?.ToString() ?? item["titleId"]?.ToString() ?? "";
                    var decimalId = ConvertTitleIdToDecimal(hexadecimalId);
                    var name = item["name"]?.ToString() ?? "Unknown title";
                    var platform = string.Join(", ", (item["systems"] as JArray ?? []).Values<string>().Select(FormatXboxPlatform).Distinct());
                    var image = Games.FirstOrDefault(game => game.Id == decimalId)?.ImageUrl;
                    return new TitleSearchResult(decimalId, name, string.IsNullOrWhiteSpace(platform) ? "Platform unavailable" : platform, image);
                })
                .OrderBy(result => result.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(result => result.Platform, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            LookupOutput = $"Found {TitleSearchResults.Length} title results.";
        }
        catch { LookupOutput = "Title search failed. Check the connection and try again."; }
        finally { Busy(false); }
    }
    internal static string ConvertTitleIdToDecimal(string hexadecimalId) =>
        uint.TryParse(hexadecimalId, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed)
            ? parsed.ToString(CultureInfo.InvariantCulture)
            : hexadecimalId;

    private static string FormatXboxPlatform(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "XBOXONE" => "Xbox One",
        "XBOXSERIES" or "SCARLETT" => "Xbox Series X|S",
        "XBOX360" => "Xbox 360",
        "PC" or "WINDOWS" => "Windows",
        "MOBILE" => "Mobile",
        "IOS" => "iOS",
        "ANDROID" => "Android",
        "SWITCH" => "Nintendo Switch",
        var other when !string.IsNullOrWhiteSpace(other) => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(other.ToLowerInvariant()),
        _ => ""
    };
    public async Task SearchGamertagAsync()
    {
        if (!CanQuery || string.IsNullOrWhiteSpace(GamertagSearch)) return;
        lookupXuid = "";
        await WithAccountAsync(async (api, _) =>
        {
            var profile = await api.GetGamertagProfileAsync(GamertagSearch);
            var user = profile?["profileUsers"]?.FirstOrDefault() ?? throw new InvalidDataException();
            lookupXuid = user["id"]?.ToString() ?? "";
            LookupOutput = user.ToString();
        });
    }
    public async Task ExportProfileGamesAsync(string path)
    {
        if (lookupXuid.Length == 0) { Notice = "Look up a gamertag first."; return; }
        await WithAccountAsync(async (api, _) =>
        {
            static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
            var titles = await api.GetGamesListAsync(lookupXuid) ?? throw new InvalidDataException();
            var lines = new List<string> { "Title ID,Title,CurrentAchievements,Gamerscore,Progress,Devices,Genres" };
            foreach (var title in titles.Titles)
            {
                if (ExcludeZeroScore && (title.Achievement?.TotalGamerscore ?? 0) == 0 || Exclude360 && title.Devices.Contains("Xbox360")) continue;
                lines.Add(string.Join(",", new[] { title.TitleId ?? "", title.Name ?? "", title.Achievement?.CurrentAchievements.ToString() ?? "", $"{title.Achievement?.CurrentGamerscore}/{title.Achievement?.TotalGamerscore}", title.Achievement?.ProgressPercentage.ToString() ?? "", string.Join(", ", title.Devices), string.Join(", ", title.Detail?.Genres ?? []) }.Select(Csv)));
            }
            await File.WriteAllLinesAsync(path, lines, new UTF8Encoding(true), lifetime.Token); Notice = $"Exported {lines.Count - 1} games.";
        });
    }
    public void OpenUtilityLink(string destination)
    {
        try
        {
            if (destination == "Steam converter") RefreshConverterStatus();
            var target = destination switch { "Discord" => "https://discord.gg/EY6AJpNfVu", "GitHub" => "https://github.com/trippyelephant/achievement-labs", "Original XAU" => "https://github.com/Fumo-Unlockers/Xbox-Achievement-Unlocker", "Steam converter" => steam.GetConverterPath(), _ => throw new InvalidOperationException() };
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch { Notice = "Could not open the requested link or workspace."; }
    }
}
