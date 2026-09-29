using Newtonsoft.Json;
using AchievementLabs.Models;
using AchievementLabs.Services.Gfwl;
using AchievementLabs.Services.Xbox360;
using AchievementLabs.Services.LegacyXbox.Win8;
using AchievementLabs.Services.LegacyXbox.Ty;
namespace AchievementLabs.Desktop;
public sealed partial class DesktopModel
{
    private Workflows.Windows8ViewModel? windows8;
    public Workflows.Windows8ViewModel Windows8 => windows8 ??= new(() => (session?.Authorization ?? "", session?.Xuid ?? ""));
    private Workflows.Win8SaveToolsViewModel? win8Tools;
    public Workflows.Win8SaveToolsViewModel Win8Tools => win8Tools ??= ObserveWorkflow(new Workflows.Win8SaveToolsViewModel(saveTools, new Workflows.NativeNotices(message => Notice = message), queueAccount));
    private Workflows.HaloLegacyViewModel? legacyBridge;
    public Workflows.HaloLegacyViewModel LegacyBridge => legacyBridge ??= ObserveWorkflow(new Workflows.HaloLegacyViewModel(new AchievementLabs.Services.LegacyXbox.LegacyXboxBridge(), new Workflows.NativeNotices(message => Notice = message), queueAccount));
    private readonly IWin8SaveTool[] saveTools = [new ColdAlleySaveTool(), new CutTheRopeWin8Tool(), new Fishdom3SaveTool(), new MicrosoftBingoLocalSyncTool(), new TyWin8SaveTool(new TySaveReader())];
    private readonly GfwlAchievementManagerService gfwl = new();
    private readonly Xbox360PackageService packages = new();
    private string legacyOutput = "Select a tool to begin.", legacyXuid = "", saveAchievementId = "1", selectedSaveGame = "", packagePath = "";
    private GfwlProcessItem[] gfwlProcesses = [];
    private GfwlProcessItem? selectedGfwlProcess;
    public bool IsLegacy => page == "Legacy";
    public string HorizonStatus => packages.IsHorizonInstalled ? "Horizon reference found: " + packages.HorizonInstallPath : "Horizon reference not installed; package inspection remains available.";
    public void RefreshHorizonStatus() { Changed(nameof(HorizonStatus)); }
    public void OpenHorizonFolder()
    {
        try
        {
            var folder = Path.GetDirectoryName(packages.HorizonInstallPath)!;
            if (!Directory.Exists(folder)) { Notice = "The Horizon reference folder was not found."; return; }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch { Notice = "Could not open the Horizon reference folder."; }
    }
    public string[] SaveGames => saveTools.Select(t => t.DisplayName).ToArray();
    public string SelectedSaveGame { get => selectedSaveGame; set { selectedSaveGame = value; Changed(); } }
    public string LegacyXuid { get => legacyXuid; set { legacyXuid = value; Changed(); } }
    public string SaveAchievementId { get => saveAchievementId; set { saveAchievementId = value; Changed(); } }
    public string PackagePath { get => packagePath; set { packagePath = value; Changed(); } }
    public string LegacyOutput { get => legacyOutput; private set { legacyOutput = value; Changed(); } }
    public GfwlProcessItem[] GfwlProcesses { get => gfwlProcesses; private set { gfwlProcesses = value; Changed(); } }
    public GfwlProcessItem? SelectedGfwlProcess { get => selectedGfwlProcess; set { selectedGfwlProcess = value; Changed(); } }
    public void OpenLegacy() { queueAccount.XAUTH = session?.Authorization ?? ""; queueAccount.XUIDOnly = session?.Xuid ?? ""; queueAccount.LastOAuthResponse = session?.OAuthResponse; LegacyBridge.OnNavigatedTo(); Win8Tools.OnNavigatedTo(); Navigate("Legacy"); if (string.IsNullOrEmpty(SelectedSaveGame)) SelectedSaveGame = SaveGames[0]; if (session != null) LegacyXuid = session.Xuid; }
    public async Task RunSaveToolAsync(string operation)
    {
        if (busy) return;
        if (!long.TryParse(LegacyXuid, out var xuid) || xuid <= 0) { LegacyOutput = "Enter the Xbox user ID for this save."; return; }
        var tool = saveTools.FirstOrDefault(t => t.DisplayName == SelectedSaveGame);
        if (tool == null) return;
        var idText = SaveAchievementId;
        await LegacyOperationAsync(async () =>
        {
            object result = await Task.Run<object>(() => operation switch
            {
                "Backup" => tool.Backup(xuid),
                "Prepare" when tool.CanPrepare && int.TryParse(idText, out var id) && id >= 1 && id <= tool.MaxAchievementId => tool.PrepareAchievement(xuid, id),
                "PrepareAll" when tool.CanPrepare => tool.PrepareAll(xuid),
                "Read" => tool.Read(xuid),
                _ => throw new InvalidDataException("Unsupported operation or achievement ID.")
            }, lifetime.Token);
            LegacyOutput = JsonConvert.SerializeObject(result, Formatting.Indented);
        });
    }
    public async Task InspectPackageAsync() => await LegacyOperationAsync(async () =>
    {
        var path = PackagePath;
        var result = await Task.Run(() => packages.InspectPackage(path), lifetime.Token);
        LegacyOutput = JsonConvert.SerializeObject(result, Formatting.Indented);
    });
    public async Task ScanGfwlAsync() => await LegacyOperationAsync(async () =>
    {
        var result = await Task.Run(() => (Status: gfwl.GetBinaryStatus(), Processes: gfwl.GetCandidateProcesses()), lifetime.Token);
        GfwlProcesses = result.Processes.ToArray(); LegacyOutput = result.Status.Summary;
    });
    public async Task InjectGfwlAsync()
    {
        var process = SelectedGfwlProcess; if (process == null) return;
        await LegacyOperationAsync(async () => { var result = await gfwl.InjectAdapterAsync(process, lifetime.Token); LegacyOutput = result.Message; });
    }
    private async Task LegacyOperationAsync(Func<Task> action)
    {
        if (busy) return; Busy(true);
        try { await action(); }
        catch (OperationCanceledException) { LegacyOutput = "Operation cancelled."; }
        catch { LegacyOutput = "Operation failed. Check the selected file, user ID, achievement ID, and required helper binaries."; }
        finally { Busy(false); }
    }
}
