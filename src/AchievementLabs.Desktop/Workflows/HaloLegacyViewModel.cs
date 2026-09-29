using System.Collections.ObjectModel;
using System.Net.Http;
using AchievementLabs.Services.LegacyXbox;

namespace AchievementLabs.Desktop.Workflows;

public partial class HaloLegacyViewModel : ObservableObject, INativePageLifecycle
{
    private readonly NativeAccountContext account;
    private readonly ILegacyXboxBridge _legacyXboxBridge;
    private readonly NativeNotices _snackbarService;
    private readonly TimeSpan _snackbarDuration = TimeSpan.FromSeconds(3);

    public HaloLegacyViewModel(ILegacyXboxBridge legacyXboxBridge, NativeNotices snackbarService, NativeAccountContext account)
    {
        this.account = account;
        _legacyXboxBridge = legacyXboxBridge;
        _snackbarService = snackbarService;
        TitleProfileNames = LegacyXboxTitleCatalog.KnownTitles.Select(profile => profile.Name).ToList();
        SelectedTitleProfileName = TitleProfileNames.FirstOrDefault() ?? "";
        UnlockDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
        UnlockTime = DateTime.UtcNow.ToString("HH:mm:ss");
        RefreshSelectedProfile();
        RefreshPreview();
    }

    public IReadOnlyList<string> TitleProfileNames { get; }
    public IReadOnlyList<string> RequestModeNames { get; } = Enum.GetNames<LegacyUnlockRequestMode>();

    [ObservableProperty] private bool _isInitialized;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _selectedTitleProfileName = "";
    [ObservableProperty] private string _titleId = "";
    [ObservableProperty] private string _xuid = "";
    [ObservableProperty] private string _authorizationHeader = "";
    [ObservableProperty] private string _msaAccessToken = "";
    [ObservableProperty] private string _achievementId = "0";
    [ObservableProperty] private int _achievementPlatform = 17;
    [ObservableProperty] private string _selectedRequestModeName = LegacyUnlockRequestMode.AchievementRecord.ToString();
    [ObservableProperty] private int _rangeStart;
    [ObservableProperty] private int _rangeEnd = 49;
    [ObservableProperty] private int _rangeDelayMilliseconds = 750;
    [ObservableProperty] private string _unlockDate = "";
    [ObservableProperty] private string _unlockTime = "";
    [ObservableProperty] private string _selectedProfileDetails = "";
    [ObservableProperty] private string _statusText = "Ready.";
    [ObservableProperty] private string _requestPreview = "";
    [ObservableProperty] private string _lastResponseDetails = "";
    [ObservableProperty] private ObservableCollection<HaloLegacyUnlockLogItem> _unlockLog = new();

    public void OnNavigatedTo()
    {
        IsInitialized = account.InitComplete;
        if (string.IsNullOrWhiteSpace(Xuid) && !string.IsNullOrWhiteSpace(account.XUIDOnly))
            Xuid = account.XUIDOnly;
    }

    public void OnNavigatedFrom()
    {
    }

    partial void OnSelectedTitleProfileNameChanged(string value)
    {
        RefreshSelectedProfile();
        RefreshPreview();
    }

    partial void OnTitleIdChanged(string value) => RefreshPreview();
    partial void OnXuidChanged(string value) => RefreshPreview();
    partial void OnAchievementIdChanged(string value) => RefreshPreview();
    partial void OnAchievementPlatformChanged(int value) => RefreshPreview();
    partial void OnSelectedRequestModeNameChanged(string value) => RefreshPreview();
    partial void OnUnlockDateChanged(string value) => RefreshPreview();
    partial void OnUnlockTimeChanged(string value) => RefreshPreview();

    [RelayCommand]
    public void UseCurrentAchievementLabsSession()
    {
        AuthorizationHeader = account.XAUTH;
        Xuid = account.XUIDOnly;
        StatusText = string.IsNullOrWhiteSpace(AuthorizationHeader)
            ? "Current app session is not signed in."
            : "Using the current app XAUTH header.";
        RefreshPreview();
    }

    [RelayCommand]
    public async Task FetchLocalTicket()
    {
        IsBusy = true;
        StatusText = "Fetching local compact ticket...";

        try
        {
            using var httpClient = new HttpClient();
            var json = await httpClient.GetStringAsync("http://127.0.0.1:8099/ticket");
            using var document = System.Text.Json.JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("ticket", out var ticketElement))
                throw new InvalidOperationException("Local ticket response did not contain a ticket field.");

            MsaAccessToken = ticketElement.GetString() ?? "";
            var account = document.RootElement.TryGetProperty("account", out var accountElement)
                ? accountElement.GetString()
                : "";
            StatusText = string.IsNullOrWhiteSpace(account)
                ? "Fetched local compact ticket."
                : $"Fetched local compact ticket for {account}.";
            LastResponseDetails = $"Fetched ticket from http://127.0.0.1:8099/ticket{Environment.NewLine}Account: {account}";
            _snackbarService.Show("Legacy Bridge", "Fetched local compact ticket.",
                NoticeAppearance.Success, new NoticeIcon(NoticeSymbol.Checkmark24), _snackbarDuration);
        }
        catch (Exception ex)
        {
            LastResponseDetails = FormatExceptionDetails(ex);
            StatusText = $"Local ticket fetch failed: {ex.Message}";
            ShowError("Could not fetch the local ticket. Make sure ticket_server.exe is running.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ProbeSignedDevices()
    {
        IsBusy = true;
        StatusText = "Probing signed device auth...";

        try
        {
            var results = await _legacyXboxBridge.ProbeSignedDeviceAuthAsync();
            LastResponseDetails = string.Join(
                Environment.NewLine + Environment.NewLine,
                results.Select(result =>
                    $"{result.DeviceType}: {(result.Success ? "OK" : "Failed")}{Environment.NewLine}{result.Details}"));

            StatusText = results.Any(result => result.DeviceType == "WindowsOneCore" && result.Success)
                ? "WindowsOneCore device auth is available."
                : "WindowsOneCore device auth is not available.";
            _snackbarService.Show("Device Probe", StatusText,
                results.Any(result => result.Success) ? NoticeAppearance.Caution : NoticeAppearance.Danger,
                new NoticeIcon(NoticeSymbol.Info24), _snackbarDuration);
        }
        catch (Exception ex)
        {
            LastResponseDetails = FormatExceptionDetails(ex);
            StatusText = $"Device probe failed: {ex.Message}";
            ShowError("Device auth probe failed. See response details.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task UseAchievementLabsMsaBridge()
    {
        var accessToken = account.LastOAuthResponse?.AccessToken;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            ShowError("No Microsoft OAuth access token is available. Log in through Achievement Labs first.");
            return;
        }

        IsBusy = true;
        StatusText = "Converting Microsoft token through legacy bridge...";

        try
        {
            var result = await _legacyXboxBridge.ConvertMsaAccessTokenAsync(accessToken);
            AuthorizationHeader = result.AuthorizationHeader;
            if (!string.IsNullOrWhiteSpace(result.Xuid))
                Xuid = result.Xuid;

            StatusText = "Using app Microsoft token via legacy user/XSTS bridge.";
            _snackbarService.Show("Legacy Bridge", "Generated a user/XSTS XBL3.0 header from app OAuth.",
                NoticeAppearance.Success, new NoticeIcon(NoticeSymbol.Checkmark24), _snackbarDuration);
            RefreshPreview();
        }
        catch (Exception ex)
        {
            StatusText = $"MSA bridge failed: {ex.Message}";
            ShowError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task UseSignedWin8Bridge()
    {
        var tokenOrTicket = !string.IsNullOrWhiteSpace(MsaAccessToken)
            ? MsaAccessToken
            : account.LastOAuthResponse?.AccessToken;
        if (string.IsNullOrWhiteSpace(tokenOrTicket))
        {
            ShowError("Paste a compact legacy RPS ticket or log in through Achievement Labs first.");
            return;
        }

        IsBusy = true;
        StatusText = "Minting signed WindowsOneCore token...";

        try
        {
            var result = await _legacyXboxBridge.ConvertMsaAccessTokenToSignedWin8Async(tokenOrTicket);
            AuthorizationHeader = result.AuthorizationHeader;
            if (!string.IsNullOrWhiteSpace(result.Xuid))
                Xuid = result.Xuid;

            SelectedRequestModeName = LegacyUnlockRequestMode.SignedAchievementRecord.ToString();
            StatusText = $"Using signed {result.DeviceType} XBL3.0 header.";
            LastResponseDetails =
                $"Signed auth minted successfully.{Environment.NewLine}" +
                $"Device: {result.DeviceType} {result.DeviceVersion} serial={result.IncludedSerialNumber}{Environment.NewLine}" +
                $"Ticket format: {result.TicketFormat}{Environment.NewLine}" +
                $"Token source: {(string.IsNullOrWhiteSpace(MsaAccessToken) ? "app OAuth access token" : "pasted legacy ticket/token")}{Environment.NewLine}" +
                $"XUID: {result.Xuid}{Environment.NewLine}" +
                $"Expires: {result.ExpiresOn}";
            _snackbarService.Show("Legacy Bridge", "Generated a signed Win8 device-bound XBL3.0 header.",
                NoticeAppearance.Success, new NoticeIcon(NoticeSymbol.Checkmark24), _snackbarDuration);
            RefreshPreview();
        }
        catch (Exception ex)
        {
            LastResponseDetails = FormatExceptionDetails(ex);
            StatusText = $"Signed Win8 bridge failed: {ex.Message}";
            ShowError("Signed Win8 bridge failed. See response details for the full error.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task UseSignedWin32Bridge()
    {
        var tokenOrTicket = !string.IsNullOrWhiteSpace(MsaAccessToken)
            ? MsaAccessToken
            : account.LastOAuthResponse?.AccessToken;
        if (string.IsNullOrWhiteSpace(tokenOrTicket))
        {
            ShowError("Paste a compact legacy RPS ticket or log in through Achievement Labs first.");
            return;
        }

        IsBusy = true;
        StatusText = "Minting signed Win32 token...";

        try
        {
            var result = await _legacyXboxBridge.ConvertMsaAccessTokenToSignedWin32Async(tokenOrTicket);
            AuthorizationHeader = result.AuthorizationHeader;
            if (!string.IsNullOrWhiteSpace(result.Xuid))
                Xuid = result.Xuid;

            SelectedRequestModeName = LegacyUnlockRequestMode.SignedAchievementRecord.ToString();
            StatusText = $"Using signed {result.DeviceType} XBL3.0 header.";
            LastResponseDetails =
                $"Signed Win32 auth minted successfully.{Environment.NewLine}" +
                $"Device: {result.DeviceType} {result.DeviceVersion} serial={result.IncludedSerialNumber}{Environment.NewLine}" +
                $"Ticket format: {result.TicketFormat}{Environment.NewLine}" +
                $"Token source: {(string.IsNullOrWhiteSpace(MsaAccessToken) ? "app OAuth access token" : "pasted legacy ticket/token")}{Environment.NewLine}" +
                $"XUID: {result.Xuid}{Environment.NewLine}" +
                $"Expires: {result.ExpiresOn}{Environment.NewLine}{Environment.NewLine}" +
                "Note: Win32 proves signed device auth works, but it may still lack the Windows 8 claim/platform authority required by legacy titles.";
            _snackbarService.Show("Legacy Bridge", "Generated a signed Win32 XBL3.0 header.",
                NoticeAppearance.Success, new NoticeIcon(NoticeSymbol.Checkmark24), _snackbarDuration);
            RefreshPreview();
        }
        catch (Exception ex)
        {
            LastResponseDetails = FormatExceptionDetails(ex);
            StatusText = $"Signed Win32 bridge failed: {ex.Message}";
            ShowError("Signed Win32 bridge failed. See response details for the full error.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ConvertMsaToken()
    {
        if (string.IsNullOrWhiteSpace(MsaAccessToken))
        {
            ShowError("Paste an MSA access token first.");
            return;
        }

        IsBusy = true;
        StatusText = "Converting MSA access token to XBL3.0...";

        try
        {
            var result = await _legacyXboxBridge.ConvertMsaAccessTokenAsync(MsaAccessToken);
            AuthorizationHeader = result.AuthorizationHeader;
            if (!string.IsNullOrWhiteSpace(result.Xuid))
                Xuid = result.Xuid;

            StatusText = $"Converted token. XUID: {(string.IsNullOrWhiteSpace(result.Xuid) ? "unknown" : result.Xuid)}";
            _snackbarService.Show("Token Converted", "Legacy token bridge generated an XBL3.0 header.",
                NoticeAppearance.Success, new NoticeIcon(NoticeSymbol.Checkmark24), _snackbarDuration);
            RefreshPreview();
        }
        catch (Exception ex)
        {
            StatusText = $"Token conversion failed: {ex.Message}";
            ShowError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SendSingleUnlock()
    {
        await SendUnlock(AchievementId);
    }

    [RelayCommand]
    public async Task ReadProfileAchievements()
    {
        await ReadAchievements(LegacyAchievementReadSource.Profile);
    }

    [RelayCommand]
    public async Task ReadProgressCatalog()
    {
        await ReadAchievements(LegacyAchievementReadSource.Progress);
    }

    [RelayCommand]
    public async Task SendRangeUnlock()
    {
        if (RangeEnd < RangeStart)
        {
            ShowError("Range end must be greater than or equal to range start.");
            return;
        }

        IsBusy = true;
        try
        {
            for (var id = RangeStart; id <= RangeEnd; id++)
            {
                AchievementId = id.ToString();
                await SendUnlock(id.ToString(), keepBusyState: true);
                if (RangeDelayMilliseconds > 0 && id < RangeEnd)
                    await Task.Delay(RangeDelayMilliseconds);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void RefreshPreview()
    {
        var profile = GetSelectedProfile();
        if (profile == null)
        {
            RequestPreview = "";
            return;
        }

        var timestamp = GetUnlockTimestampUtc();
        var requestMode = GetSelectedRequestMode();
        var previewXuid = string.IsNullOrWhiteSpace(Xuid) ? "{xuid}" : Xuid;
        var previewAchievementId = string.IsNullOrWhiteSpace(AchievementId) ? "{achievementId}" : AchievementId;
        var previewTitleId = string.IsNullOrWhiteSpace(TitleId) ? "{titleId}" : TitleId;
        var requestUri = BuildPreviewRequestUri(profile, previewXuid, previewTitleId, previewAchievementId, requestMode);
        var requestBody = BuildPreviewRequestBody(profile, previewXuid, previewTitleId, previewAchievementId, timestamp, requestMode);

        RequestPreview = $"{requestUri}{Environment.NewLine}{Environment.NewLine}{requestBody}";
    }

    private async Task SendUnlock(string achievementId, bool keepBusyState = false)
    {
        var profile = GetSelectedProfile();
        if (profile == null)
        {
            ShowError("Select a legacy title profile.");
            return;
        }

        if (string.IsNullOrWhiteSpace(AuthorizationHeader) ||
            string.IsNullOrWhiteSpace(Xuid) ||
            string.IsNullOrWhiteSpace(TitleId) ||
            string.IsNullOrWhiteSpace(achievementId))
        {
            ShowError("Authorization, XUID, title ID, and achievement ID are required.");
            return;
        }

        if (!keepBusyState)
            IsBusy = true;

        StatusText = $"Sending achievement {achievementId}...";

        try
        {
            var result = await _legacyXboxBridge.SendProgressUnlockAsync(
                profile,
                AuthorizationHeader,
                Xuid,
                TitleId,
                achievementId,
                AchievementPlatform,
                GetUnlockTimestampUtc(),
                GetSelectedRequestMode());

            LastResponseDetails =
                $"HTTP {result.StatusCode} {result.ReasonPhrase}{Environment.NewLine}{result.RequestUri}{Environment.NewLine}{Environment.NewLine}{result.RequestBody}{Environment.NewLine}{Environment.NewLine}{result.ResponseBody}";

            UnlockLog.Insert(0, new HaloLegacyUnlockLogItem
            {
                AchievementId = achievementId,
                TitleId = TitleId,
                StatusCode = result.StatusCode,
                Result = result.StatusCode == 409 && result.ResponseBody.Contains("\"code\":19", StringComparison.OrdinalIgnoreCase)
                    ? "Already unlocked"
                    : result.IsSuccess ? "Success" : "Failed",
                ResponsePreview = Truncate(result.ResponseBody, 240),
                CompletedAt = DateTime.Now.ToString("HH:mm:ss")
            });

            StatusText = result.StatusCode == 409 && result.ResponseBody.Contains("\"code\":19", StringComparison.OrdinalIgnoreCase)
                ? $"Achievement {achievementId} is already unlocked."
                : result.IsSuccess
                ? $"Achievement {achievementId} sent successfully."
                : $"Achievement {achievementId} failed: HTTP {result.StatusCode} {result.ReasonPhrase}";

            if (!result.IsSuccess)
                ShowError(StatusText);
        }
        catch (Exception ex)
        {
            UnlockLog.Insert(0, new HaloLegacyUnlockLogItem
            {
                AchievementId = achievementId,
                TitleId = TitleId,
                StatusCode = 0,
                Result = "Error",
                ResponsePreview = ex.Message,
                CompletedAt = DateTime.Now.ToString("HH:mm:ss")
            });
            LastResponseDetails = ex.ToString();
            StatusText = $"Achievement {achievementId} errored: {ex.Message}";
            ShowError(ex.Message);
        }
        finally
        {
            if (!keepBusyState)
                IsBusy = false;
        }
    }

    private async Task ReadAchievements(LegacyAchievementReadSource source)
    {
        if (string.IsNullOrWhiteSpace(AuthorizationHeader) ||
            string.IsNullOrWhiteSpace(Xuid) ||
            string.IsNullOrWhiteSpace(TitleId))
        {
            ShowError("Authorization, XUID, and title ID are required.");
            return;
        }

        IsBusy = true;
        StatusText = source == LegacyAchievementReadSource.Profile
            ? "Reading achievements from Xbox profile..."
            : "Reading legacy progress catalog...";

        try
        {
            var result = await _legacyXboxBridge.ReadAchievementsAsync(
                AuthorizationHeader,
                Xuid,
                TitleId,
                source);

            LastResponseDetails =
                $"HTTP {result.StatusCode} {result.ReasonPhrase}{Environment.NewLine}{result.RequestUri}{Environment.NewLine}{Environment.NewLine}{result.ResponseBody}";

            UnlockLog.Insert(0, new HaloLegacyUnlockLogItem
            {
                AchievementId = source == LegacyAchievementReadSource.Profile ? "profile" : "progress",
                TitleId = TitleId,
                StatusCode = result.StatusCode,
                Result = result.IsSuccess ? "Read OK" : "Read Failed",
                ResponsePreview = Truncate(result.ResponseBody, 240),
                CompletedAt = DateTime.Now.ToString("HH:mm:ss")
            });

            StatusText = result.IsSuccess
                ? $"Read succeeded: HTTP {result.StatusCode} {result.ReasonPhrase}"
                : $"Read failed: HTTP {result.StatusCode} {result.ReasonPhrase}";

            if (!result.IsSuccess)
                ShowError(StatusText);
        }
        catch (Exception ex)
        {
            LastResponseDetails = ex.ToString();
            StatusText = $"Read errored: {ex.Message}";
            ShowError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private LegacyXboxTitleProfile? GetSelectedProfile()
    {
        return LegacyXboxTitleCatalog.KnownTitles.FirstOrDefault(profile => profile.Name == SelectedTitleProfileName)
               ?? LegacyXboxTitleCatalog.KnownTitles.FirstOrDefault();
    }

    private void RefreshSelectedProfile()
    {
        var profile = GetSelectedProfile();
        if (profile == null)
            return;

        TitleId = profile.ProductionTitleIdDecimal ?? "";
        AchievementPlatform = profile.AchievementPlatform;

        SelectedProfileDetails =
            $"{profile.Platform} | Bundle: {profile.BundleId} | Scheme: {profile.UrlScheme} | Payload platform: {profile.AchievementPlatform} | SCID: {profile.ServiceConfigId}";
    }

    private LegacyUnlockRequestMode GetSelectedRequestMode()
    {
        return Enum.TryParse<LegacyUnlockRequestMode>(SelectedRequestModeName, out var requestMode)
            ? requestMode
            : LegacyUnlockRequestMode.AchievementRecord;
    }

    private string BuildPreviewRequestUri(
        LegacyXboxTitleProfile profile,
        string xuid,
        string titleId,
        string achievementId,
        LegacyUnlockRequestMode requestMode)
    {
        return requestMode switch
        {
            LegacyUnlockRequestMode.PercentCompletePost or LegacyUnlockRequestMode.ProgressUpdatePost =>
                $"https://progress.xboxlive.com/users/xuid({xuid})/progress/achievements",
            LegacyUnlockRequestMode.TitleAchievementProgressUpdatePost =>
                $"https://progress.xboxlive.com/users/xuid({xuid})/progress/titleachievements",
            LegacyUnlockRequestMode.TitleAchievementRecord =>
                $"https://progress.xboxlive.com/users/xuid({xuid})/progress/titleachievements/{achievementId}?titleId={titleId}",
            LegacyUnlockRequestMode.PercentCompletePut =>
                $"https://progress.xboxlive.com/users/xuid({xuid})/progress/achievements/{achievementId}?titleId={titleId}",
            LegacyUnlockRequestMode.AchievementServiceUpdate =>
                $"https://achievements.xboxlive.com/users/xuid({xuid})/achievements",
            LegacyUnlockRequestMode.AchievementServiceScidUpdate when !string.IsNullOrWhiteSpace(profile.ServiceConfigId) =>
                $"https://achievements.xboxlive.com/users/xuid({xuid})/achievements/{profile.ServiceConfigId.ToLowerInvariant()}/update",
            LegacyUnlockRequestMode.AchievementServiceScidUpdate =>
                $"https://achievements.xboxlive.com/users/xuid({xuid})/achievements",
            _ => profile.ProgressEndpointTemplate
                .Replace("{xuid}", xuid)
                .Replace("{achievementId}", achievementId)
                .Replace("{titleId}", titleId)
        };
    }

    private string BuildPreviewRequestBody(
        LegacyXboxTitleProfile profile,
        string xuid,
        string titleId,
        string achievementId,
        DateTime timestamp,
        LegacyUnlockRequestMode requestMode)
    {
        return requestMode switch
        {
            LegacyUnlockRequestMode.PercentCompletePost =>
                $"{{\"titleId\":{JsonNumberOrString(titleId)},\"achievements\":[{{\"id\":{JsonNumberOrString(achievementId)},\"percentComplete\":100}}]}}",
            LegacyUnlockRequestMode.PercentCompletePut =>
                "{\"percentComplete\":100}",
            LegacyUnlockRequestMode.ProgressUpdatePost =>
                BuildProgressUpdatePreviewBody(profile, xuid, titleId, achievementId, useStringAchievementId: false, includeServiceConfigId: true),
            LegacyUnlockRequestMode.TitleAchievementProgressUpdatePost =>
                BuildProgressUpdatePreviewBody(profile, xuid, titleId, achievementId, useStringAchievementId: false, includeServiceConfigId: true),
            LegacyUnlockRequestMode.AchievementServiceUpdate =>
                BuildProgressUpdatePreviewBody(profile, xuid, titleId, achievementId, useStringAchievementId: false, includeServiceConfigId: false),
            LegacyUnlockRequestMode.AchievementServiceScidUpdate =>
                BuildProgressUpdatePreviewBody(profile, xuid, titleId, achievementId, useStringAchievementId: true, includeServiceConfigId: true),
            LegacyUnlockRequestMode.AchievementRecord or LegacyUnlockRequestMode.SignedAchievementRecord or LegacyUnlockRequestMode.TitleAchievementRecord => profile.UnlockPayloadTemplate
                .Replace("{achievementId}", achievementId)
                .Replace("{titleId}", titleId)
                .Replace("{platform}", AchievementPlatform.ToString())
                .Replace("{timestamp}", timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")),
            _ => profile.UnlockPayloadTemplate
        };
    }

    private static string BuildProgressUpdatePreviewBody(
        LegacyXboxTitleProfile profile,
        string xuid,
        string titleId,
        string achievementId,
        bool useStringAchievementId,
        bool includeServiceConfigId)
    {
        var serviceConfig = !includeServiceConfigId || string.IsNullOrWhiteSpace(profile.ServiceConfigId)
            ? ""
            : $",\"serviceConfigId\":\"{EscapeJson(profile.ServiceConfigId.ToLowerInvariant())}\"";
        var achievementIdValue = useStringAchievementId
            ? $"\"{EscapeJson(achievementId)}\""
            : JsonNumberOrString(achievementId);

        return $"{{\"action\":\"progressUpdate\",\"titleId\":{JsonNumberOrString(titleId)},\"userId\":\"{EscapeJson(xuid)}\"{serviceConfig},\"achievements\":[{{\"id\":{achievementIdValue},\"percentComplete\":100}}]}}";
    }

    private static string JsonNumberOrString(string value)
    {
        return long.TryParse(value, out _) ? value : $"\"{EscapeJson(value)}\"";
    }

    private static string EscapeJson(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private DateTime GetUnlockTimestampUtc()
    {
        var dateText = string.IsNullOrWhiteSpace(UnlockDate)
            ? DateTime.UtcNow.ToString("yyyy-MM-dd")
            : UnlockDate.Trim();
        var timeText = string.IsNullOrWhiteSpace(UnlockTime)
            ? DateTime.UtcNow.ToString("HH:mm:ss")
            : UnlockTime.Trim();

        if (DateTime.TryParse($"{dateText} {timeText}", out var parsed))
            return DateTime.SpecifyKind(parsed, DateTimeKind.Local).ToUniversalTime();

        return DateTime.UtcNow;
    }

    private void ShowError(string message)
    {
            _snackbarService.Show("Win 8 Unlocker", message,
            NoticeAppearance.Danger, new NoticeIcon(NoticeSymbol.ErrorCircle24), _snackbarDuration);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value;

        return value.Substring(0, maxLength) + "...";
    }

    private static string FormatExceptionDetails(Exception ex)
    {
        var lines = new List<string>();
        for (var current = ex; current != null; current = current.InnerException)
            lines.Add($"{current.GetType().FullName}: {current.Message}{Environment.NewLine}{current.StackTrace}");

        return string.Join($"{Environment.NewLine}{Environment.NewLine}Caused by:{Environment.NewLine}", lines);
    }
}

public sealed partial class HaloLegacyUnlockLogItem : ObservableObject
{
    [ObservableProperty] private string _completedAt = "";
    [ObservableProperty] private string _achievementId = "";
    [ObservableProperty] private string _titleId = "";
    [ObservableProperty] private int _statusCode;
    [ObservableProperty] private string _result = "";
    [ObservableProperty] private string _responsePreview = "";
}
