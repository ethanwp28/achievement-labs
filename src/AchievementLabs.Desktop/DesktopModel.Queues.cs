using AchievementLabs.Desktop.Workflows;
namespace AchievementLabs.Desktop;
public sealed partial class DesktopModel
{
    private readonly NativeAccountContext queueAccount = new();
    private AutoUnlockerViewModel? xboxQueue;
    private SteamAutoUnlockerViewModel? steamQueue;
    public AutoUnlockerViewModel XboxQueue => xboxQueue ??= new(new NativeNotices(message => Notice = message), queueAccount) { EventCatalog = eventCatalog };
    public SteamAutoUnlockerViewModel SteamQueue => steamQueue ??= ObserveWorkflow(new SteamAutoUnlockerViewModel(steam, new NativeNotices(message => Notice = message)));
    public bool IsQueues => page == "Queues";
    private bool queueActive;
    public bool QueueActive { get => queueActive; private set { queueActive = value; Changed(); Busy(busy); } }
    public void OpenQueues()
    {
        queueAccount.LastOAuthResponse = session?.OAuthResponse; queueAccount.XAUTH = session?.Authorization ?? ""; queueAccount.XUIDOnly = session?.Xuid ?? ""; queueAccount.EventsToken = session?.EventsToken ?? ""; queueAccount.EventsDirectory = EventsDirectory;
        XboxQueue.OnNavigatedTo(); Navigate("Queues");
    }
    public async Task RunXboxQueueAsync()
    {
        if (QueueActive && !XboxQueue.IsRunning) return;
        if (session == null && !XboxQueue.IsRunning) return;
        if (XboxQueue.IsRunning) { await XboxQueue.StartStopAutoUnlock(); return; }
        if (busy || PresenceRunning) { Notice = "Wait for the current operation and stop presence before starting a queue."; return; }
        QueueActive = true;
        try { await XboxQueue.StartStopAutoUnlock(); } finally { QueueActive = false; }
    }
    public async Task RunSteamQueueAsync()
    {
        if (QueueActive || busy) return;
        QueueActive = true;
        try { await SteamQueue.StartCommand.ExecuteAsync(null); } finally { QueueActive = false; }
    }
    private void StopQueues()
    {
        steamQueue?.StopCommand.Execute(null);
        if (xboxQueue?.IsRunning == true) _ = xboxQueue.StartStopAutoUnlock();
    }
}
