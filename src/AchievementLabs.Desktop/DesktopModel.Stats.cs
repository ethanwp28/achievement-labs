using Newtonsoft.Json.Linq;
using System.Diagnostics;
namespace AchievementLabs.Desktop;
public sealed partial class DesktopModel
{
    private Workflows.StatsEditorViewModel? statsEditor;
    public Workflows.StatsEditorViewModel StatsEditor => statsEditor ??= ObserveWorkflow(new Workflows.StatsEditorViewModel(new Workflows.NativeNotices(message => Notice = message), queueAccount));
    public void OpenStats() { queueAccount.XAUTH = session?.Authorization ?? ""; queueAccount.XUIDOnly = session?.Xuid ?? ""; queueAccount.EventsToken = session?.EventsToken ?? ""; queueAccount.EventsDirectory = EventsDirectory; StatsEditor.OnNavigatedTo(); Navigate("Stats"); }
    private string statsTitleId = "", serviceConfigId = "", statName = "", statValue = "0", statsOutput = "", presenceTitleId = "", spoofTitleName = "No title selected", spoofTitleDetails = "Enter a title ID to preview its Xbox metadata.", spoofingStatus = "Spoofing not started";
    private string? spoofTitleImageUrl;
    private Task? autoPresenceTask;
    private CancellationTokenSource? presenceCancellation;
    public bool IsStats => page == "Stats";
    public string StatsTitleId { get => statsTitleId; set { statsTitleId = value ?? ""; Changed(); } }
    public string ServiceConfigId { get => serviceConfigId; set { serviceConfigId = value ?? ""; Changed(); } }
    public string StatName { get => statName; set { statName = value ?? ""; Changed(); } }
    public string StatValue { get => statValue; set { statValue = value ?? ""; Changed(); } }
    public string StatsOutput { get => statsOutput; private set { statsOutput = value; Changed(); } }
    public string PresenceTitleId { get => presenceTitleId; set { presenceTitleId = value ?? ""; Changed(); } }
    public bool PresenceRunning => presenceCancellation != null;
    public string SpoofTitleName { get => spoofTitleName; private set { spoofTitleName = value; Changed(); } }
    public string SpoofTitleDetails { get => spoofTitleDetails; private set { spoofTitleDetails = value; Changed(); } }
    public string? SpoofTitleImageUrl { get => spoofTitleImageUrl; private set { spoofTitleImageUrl = value; Changed(); } }
    public string SpoofingStatus { get => spoofingStatus; private set { spoofingStatus = value; Changed(); } }
    public void OpenSpoofer() => Navigate("Spoofer");
    public async Task LookupSpoofTitleAsync() => await WithAccountAsync(async (api, xuid) =>
    {
        if (!uint.TryParse(PresenceTitleId, out var id) || id == 0) throw new InvalidDataException();
        var response = await api.GetGameTitleAsync(xuid, id.ToString()) ?? throw new InvalidDataException();
        var title = response.Titles.FirstOrDefault() ?? throw new InvalidDataException();
        SpoofTitleName = title.Name;
        SpoofTitleImageUrl = ResolveTitleImage(title.DisplayImage, title.Images);
        SpoofTitleDetails = $"Title ID: {title.TitleId}\nPFN: {title.Pfn ?? "Unknown"}\nType: {title.Type ?? "Unknown"}\nDevices: {string.Join(", ", title.Devices)}\nGamerscore: {title.Achievement?.CurrentGamerscore ?? 0}/{title.Achievement?.TotalGamerscore ?? 0}";
    });
    public void UseSelectedTitleForStats() { StatsTitleId = SelectedGame?.Id ?? ""; ServiceConfigId = definitions.Values.FirstOrDefault()?.serviceConfigId ?? ""; OpenStats(); StatsEditor.TitleId = StatsTitleId; }
    public async Task ReadStatsAsync() => await WithAccountAsync(async (api, xuid) =>
    {
        if (!uint.TryParse(StatsTitleId, out _)) throw new InvalidDataException();
        if (string.IsNullOrWhiteSpace(ServiceConfigId)) ServiceConfigId = await api.GetTitleServiceConfigIdAsync(xuid, StatsTitleId) ?? throw new InvalidDataException();
        var response = await api.GetAllGameStatsRawAsync(xuid, ServiceConfigId);
        if (response.StatusCode < 200 || response.StatusCode >= 300) throw new HttpRequestException();
        try { StatsOutput = JToken.Parse(response.Body).ToString(); } catch { StatsOutput = response.Body; }
    });
    public async Task WriteStatAsync() => await WithAccountAsync(async (api, xuid) =>
    {
        if (!Guid.TryParse(ServiceConfigId, out _) || string.IsNullOrWhiteSpace(StatName)) throw new InvalidDataException();
        var value = JToken.Parse(StatValue);
        if (value is not JValue || value.Type == JTokenType.Null) throw new InvalidDataException();
        var response = await api.WriteTitleStatWithDiagnosticsAsync(xuid, ServiceConfigId, StatName, value);
        StatsOutput = $"Stat write HTTP {response.StatusCode}. Read stats again to verify the value.";
    });
    public async Task StartPresenceAsync()
    {
        if (!CanQuery || PresenceRunning || client == null || session == null || !uint.TryParse(PresenceTitleId, out var id) || id == 0) return;
        var presenceAuthorization = await AchievementLabs.Core.XboxPcAppAuthorizationReader.TryReadAsync(lifetime.Token);
        if (string.IsNullOrWhiteSpace(presenceAuthorization))
        {
            StatsOutput = "Could not read the Xbox PC app authorization. Open and sign in to the Xbox app, then try again.";
            return;
        }
        using var api = new XboxApiClient(presenceAuthorization, RegionOverride);
        var xuid = session.Xuid; var titleId = id.ToString();
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        presenceCancellation = cancellation; Changed(nameof(PresenceRunning)); Changed(nameof(CanDisconnect));
        var stopwatch = Stopwatch.StartNew();
        var lastHeartbeat = TimeSpan.Zero;
        try
        {
            while (true)
            {
                await requests.WaitAsync(cancellation.Token);
                try
                {
                    var result = await api.SendHeartbeatAsync(xuid, titleId);
                    StatsOutput = $"Presence heartbeat for {titleId}: HTTP {result.StatusCode}";
                    if (result.StatusCode < 200 || result.StatusCode >= 300) break;
                    lastHeartbeat = stopwatch.Elapsed;
                }
                finally { requests.Release(); }
                while (stopwatch.Elapsed - lastHeartbeat < TimeSpan.FromMinutes(5))
                {
                    SpoofingStatus = $"Spoofing {SpoofTitleName} for {stopwatch.Elapsed:hh\\:mm\\:ss}";
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellation.Token);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch { StatsOutput = "Presence heartbeat failed."; }
        finally
        {
            await requests.WaitAsync();
            try { if (!lifetime.IsCancellationRequested) await api.StopHeartbeatAsync(xuid); }
            catch { StatsOutput = "Presence stop request failed; the service may retain presence until it expires."; }
            finally { requests.Release(); presenceCancellation = null; cancellation.Dispose(); SpoofingStatus = "Spoofing not started"; Changed(nameof(PresenceRunning)); Changed(nameof(CanDisconnect)); }
        }
    }
    public void StopPresence() => presenceCancellation?.Cancel();
    private async Task WithAccountAsync(Func<XboxApiClient, string, Task> operation)
    {
        if (!CanQuery || client == null || session == null) return;
        Busy(true);
        try
        {
            await requests.WaitAsync(lifetime.Token);
            try { if (client != null && session != null) await operation(client, session.Xuid); }
            finally { requests.Release(); }
        }
        catch (OperationCanceledException) { }
        catch { Notice = StatsOutput = "Request failed. Check the account connection and request inputs."; }
        finally { Busy(false); }
    }
}
