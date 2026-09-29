using System.Diagnostics;
using AchievementLabs.Services.HttpServer;
namespace AchievementLabs.Desktop;
public sealed partial class DesktopModel
{
    public bool AllowLanServer { get; set; }
    private HttpServer? apiServer;
    private string serverPort = "1337", serverAddress = "Stopped";
    public string ServerPort { get => serverPort; set { serverPort = value; Changed(); } }
    public string ServerAddress { get => serverAddress; private set { serverAddress = value; Changed(); } }
    public void ToggleApiServer()
    {
        apiServer?.Stop();
        ServerAddress = "Disabled";
        Notice = "The legacy local API is disabled because it exposes account credentials without authentication.";
    }

    public void OpenApiAddress() { try { if (apiServer?.IsRunning == true) Process.Start(new ProcessStartInfo(apiServer.GetListeningAddress()) { UseShellExecute = true }); } catch { Notice = "Could not open the API address."; } }
    public void RestartElevated() { try { new HttpServer("1337", []).RestartAsAdmin(); } catch { Notice = "The app was not restarted."; } }
}
