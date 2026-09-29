using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using AchievementLabs.Services.LegacyXbox.Win8;

namespace AchievementLabs.Desktop.Workflows;

public sealed partial class Win8SaveAchievementRow : ObservableObject
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Evidence { get; init; } = string.Empty;
    public string StatusText => LocallySatisfied ? "Prepared" : "Not Ready";
    public bool LocallySatisfied { get; init; }
}

public sealed partial class Win8SaveFileRow : ObservableObject
{
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string Length { get; init; } = string.Empty;
    public string LastWriteTime { get; init; } = string.Empty;
    public string Sha256 { get; init; } = string.Empty;
}

public partial class Win8SaveToolsViewModel : ObservableObject, INativePageLifecycle
{
    private readonly NativeAccountContext account;
    private readonly IReadOnlyList<IWin8SaveTool> _tools;
    private readonly NativeNotices _snackbarService;
    private readonly TimeSpan _snackbarDuration = TimeSpan.FromSeconds(3);

    public Win8SaveToolsViewModel(IEnumerable<IWin8SaveTool> tools, NativeNotices snackbarService, NativeAccountContext account)
    {
        this.account = account;
        _tools = tools.OrderBy(tool => tool.DisplayName).ToList();
        _snackbarService = snackbarService;

        GameNames = _tools.Select(tool => tool.DisplayName).ToList();
        SelectedGameName = GameNames.FirstOrDefault() ?? string.Empty;
        Xuid = string.IsNullOrWhiteSpace(account.XUIDOnly) ? "" : account.XUIDOnly;
        RefreshSavePath();
    }

    public IReadOnlyList<string> GameNames { get; }

    [ObservableProperty] private string _selectedGameName = string.Empty;
    [ObservableProperty] private string _xuid = string.Empty;
    [ObservableProperty] private string _savePath = string.Empty;
    [ObservableProperty] private string _statusText = "Select a Windows title and refresh its local save.";
    [ObservableProperty] private string _summaryText = "No save loaded.";
    [ObservableProperty] private string _formatStatus = "Unknown";
    [ObservableProperty] private string _selectedAchievementId = "1";
    [ObservableProperty] private bool _canPrepare;
    [ObservableProperty] private bool _canRuntimeSubmit;
    [ObservableProperty] private bool _isRuntimeSubmitting;
    [ObservableProperty] private ObservableCollection<Win8SaveAchievementRow> _achievements = [];
    [ObservableProperty] private ObservableCollection<Win8SaveFileRow> _files = [];

    public bool CanSubmitRuntime => CanRuntimeSubmit && !IsRuntimeSubmitting;

    public void OnNavigatedTo()
    {
        if (!string.IsNullOrWhiteSpace(account.XUIDOnly) && Xuid != account.XUIDOnly)
            Xuid = account.XUIDOnly;

        RefreshSavePath();
    }

    public void OnNavigatedFrom()
    {
    }

    partial void OnXuidChanged(string value)
    {
        RefreshSavePath();
    }

    partial void OnSelectedGameNameChanged(string value)
    {
        RefreshSavePath();
        CanPrepare = GetSelectedTool()?.CanPrepare == true;
        RefreshRuntimeSubmitState();
    }

    partial void OnCanRuntimeSubmitChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSubmitRuntime));
        SubmitRuntimeSelectedCommand.NotifyCanExecuteChanged();
        SubmitRuntimeAllCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsRuntimeSubmittingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSubmitRuntime));
        SubmitRuntimeSelectedCommand.NotifyCanExecuteChanged();
        SubmitRuntimeAllCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void UseAchievementLabsXuid()
    {
        if (string.IsNullOrWhiteSpace(account.XUIDOnly))
        {
            _snackbarService.Show("No XUID", "Sign in or attach first, then try again.", NoticeAppearance.Caution, new NoticeIcon(NoticeSymbol.Warning24), _snackbarDuration);
            return;
        }

        Xuid = account.XUIDOnly;
    }

    [RelayCommand]
    private void Refresh()
    {
        try
        {
            var tool = GetRequiredTool();
            var xuid = GetRequiredXuid();
            var snapshot = tool.Read(xuid);

            SavePath = snapshot.SavePath;
            FormatStatus = snapshot.FormatStatus;
            SummaryText = snapshot.Summary;
            CanPrepare = tool.CanPrepare;
            StatusText = snapshot.Exists ? $"Loaded {snapshot.GameName} save state." : snapshot.Summary;

            Achievements = new ObservableCollection<Win8SaveAchievementRow>(
                snapshot.Achievements.Select(achievement => new Win8SaveAchievementRow
                {
                    Id = achievement.Id,
                    Name = achievement.DisplayName,
                    Evidence = achievement.Evidence,
                    LocallySatisfied = achievement.LocallySatisfied
                }));

            Files = new ObservableCollection<Win8SaveFileRow>(
                snapshot.Files.Select(file => new Win8SaveFileRow
                {
                    Name = Path.GetFileName(file.Path),
                    Path = file.Path,
                    Length = $"{file.Length:N0} bytes",
                    LastWriteTime = file.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    Sha256 = file.Sha256
                }));

            _snackbarService.Show("Local Save Loaded", SummaryText, NoticeAppearance.Success, new NoticeIcon(NoticeSymbol.Checkmark24), _snackbarDuration);
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            _snackbarService.Show("Win8 Save Error", ex.Message, NoticeAppearance.Danger, new NoticeIcon(NoticeSymbol.ErrorCircle24), TimeSpan.FromSeconds(6));
        }
    }

    [RelayCommand]
    private void BackupSave()
    {
        try
        {
            var backupPath = GetRequiredTool().Backup(GetRequiredXuid());
            StatusText = $"Backup created: {backupPath}";
            _snackbarService.Show("Backup Created", backupPath, NoticeAppearance.Success, new NoticeIcon(NoticeSymbol.Save24), TimeSpan.FromSeconds(6));
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            _snackbarService.Show("Backup Failed", ex.Message, NoticeAppearance.Danger, new NoticeIcon(NoticeSymbol.ErrorCircle24), TimeSpan.FromSeconds(6));
        }
    }

    [RelayCommand(CanExecute = nameof(CanPrepare))]
    private void PrepareSelected()
    {
        try
        {
            var tool = GetRequiredTool();
            if (!int.TryParse(SelectedAchievementId, out var achievementId) || achievementId < 1 || achievementId > tool.MaxAchievementId)
            {
                _snackbarService.Show("Invalid Achievement", $"Enter an achievement ID from 1 to {tool.MaxAchievementId}.", NoticeAppearance.Danger, new NoticeIcon(NoticeSymbol.ErrorCircle24), _snackbarDuration);
                return;
            }

            var result = tool.PrepareAchievement(GetRequiredXuid(), achievementId);
            ReportPatchResult(result, tool.DisplayName);
            Refresh();
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            _snackbarService.Show("Prepare Failed", ex.Message, NoticeAppearance.Danger, new NoticeIcon(NoticeSymbol.ErrorCircle24), TimeSpan.FromSeconds(6));
        }
    }

    [RelayCommand(CanExecute = nameof(CanPrepare))]
    private void PrepareAll()
    {
        try
        {
            var tool = GetRequiredTool();
            var result = tool.PrepareAll(GetRequiredXuid());
            ReportPatchResult(result, tool.DisplayName);
            Refresh();
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            _snackbarService.Show("Prepare Failed", ex.Message, NoticeAppearance.Danger, new NoticeIcon(NoticeSymbol.ErrorCircle24), TimeSpan.FromSeconds(6));
        }
    }

    [RelayCommand(CanExecute = nameof(CanSubmitRuntime))]
    private async Task SubmitRuntimeSelected()
    {
        try
        {
            var tool = GetRequiredTool();
            if (!IsColdAlley(tool))
            {
                _snackbarService.Show("Unsupported Runtime Submit", "Runtime submit is currently available for Cold Alley only.", NoticeAppearance.Caution, new NoticeIcon(NoticeSymbol.Warning24), _snackbarDuration);
                return;
            }

            if (!int.TryParse(SelectedAchievementId, out var achievementId) || achievementId < 1 || achievementId > tool.MaxAchievementId)
            {
                _snackbarService.Show("Invalid Achievement", $"Enter an achievement ID from 1 to {tool.MaxAchievementId}.", NoticeAppearance.Danger, new NoticeIcon(NoticeSymbol.ErrorCircle24), _snackbarDuration);
                return;
            }

            await RunColdAlleyRuntimeSubmitAsync([achievementId], submitAll: false);
            Refresh();
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            _snackbarService.Show("Runtime Submit Failed", ex.Message, NoticeAppearance.Danger, new NoticeIcon(NoticeSymbol.ErrorCircle24), TimeSpan.FromSeconds(8));
        }
    }

    [RelayCommand(CanExecute = nameof(CanSubmitRuntime))]
    private async Task SubmitRuntimeAll()
    {
        try
        {
            var tool = GetRequiredTool();
            if (!IsColdAlley(tool))
            {
                _snackbarService.Show("Unsupported Runtime Submit", "Runtime submit is currently available for Cold Alley only.", NoticeAppearance.Caution, new NoticeIcon(NoticeSymbol.Warning24), _snackbarDuration);
                return;
            }

            await RunColdAlleyRuntimeSubmitAsync([], submitAll: true);
            Refresh();
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            _snackbarService.Show("Runtime Submit Failed", ex.Message, NoticeAppearance.Danger, new NoticeIcon(NoticeSymbol.ErrorCircle24), TimeSpan.FromSeconds(8));
        }
    }

    [RelayCommand]
    private void CopyReport()
    {
        var lines = new List<string>
        {
            "Win 8 Save Editor Diagnostics",
            $"Game: {SelectedGameName}",
            $"XUID: {Xuid}",
            $"Save: {SavePath}",
            $"Format: {FormatStatus}",
            SummaryText,
            ""
        };

        lines.AddRange(Achievements.Select(row => $"{row.Id}. {row.Name} [{row.StatusText}] - {row.Evidence}"));
        lines.Add("");
        lines.AddRange(Files.Select(row => $"{row.Name} | {row.Length} | {row.LastWriteTime} | {row.Sha256}"));

        NativeClipboard.SetText(string.Join(Environment.NewLine, lines));
        _snackbarService.Show("Copied", "Local save diagnostics report copied to clipboard.", NoticeAppearance.Success, new NoticeIcon(NoticeSymbol.Clipboard24), _snackbarDuration);
    }

    private void ReportPatchResult(Win8SavePatchResult result, string gameName)
    {
        if (result.ChangedSettings.Count == 0)
        {
            StatusText = "No save changes were needed; selected trigger state was already prepared.";
            _snackbarService.Show("No Changes Needed", StatusText, NoticeAppearance.Caution, new NoticeIcon(NoticeSymbol.Warning24), TimeSpan.FromSeconds(5));
            return;
        }

        StatusText = $"Prepared {gameName} achievements {string.Join(", ", result.PreparedAchievementIds)}. Backup: {result.BackupPath}. Changes: {string.Join("; ", result.ChangedSettings)}";
        _snackbarService.Show("Local Save Prepared", $"Changed {result.ChangedSettings.Count} field(s). Launch the game while signed in so it can process its own SDK unlocks.", NoticeAppearance.Success, new NoticeIcon(NoticeSymbol.Checkmark24), TimeSpan.FromSeconds(8));
    }

    private void RefreshSavePath()
    {
        if (long.TryParse(Xuid, out var xuid))
            SavePath = GetSelectedTool()?.GetPrimarySavePath(xuid) ?? string.Empty;

        CanPrepare = GetSelectedTool()?.CanPrepare == true;
        RefreshRuntimeSubmitState();
        PrepareSelectedCommand.NotifyCanExecuteChanged();
        PrepareAllCommand.NotifyCanExecuteChanged();
    }

    private void RefreshRuntimeSubmitState()
    {
        CanRuntimeSubmit = GetSelectedTool() is { } tool && IsColdAlley(tool);
    }

    private async Task RunColdAlleyRuntimeSubmitAsync(IReadOnlyList<int> achievementIds, bool submitAll)
    {
        var scriptPath = FindColdAlleySubmitScriptPath();
        var arguments = new List<string>
        {
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            Quote(scriptPath),
            "-Launch",
            "-WaitSeconds",
            submitAll ? "45" : "25"
        };

        if (submitAll)
        {
            arguments.Add("-All");
        }
        else
        {
            arguments.Add("-AchievementId");
            arguments.Add(string.Join(",", achievementIds));
        }

        IsRuntimeSubmitting = true;
        StatusText = submitAll
            ? "Submitting all Cold Alley achievements through the running game..."
            : $"Submitting Cold Alley achievement {string.Join(", ", achievementIds)} through the running game...";

        try
        {
            var result = await RunProcessAsync("powershell.exe", string.Join(" ", arguments));
            var summaryLines = result.Output
                .Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries)
                .Where(line => line.Contains("[coldalley-submit:summary]", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (result.ExitCode != 0)
                throw new InvalidOperationException($"Cold Alley runtime submit exited with code {result.ExitCode}. {result.Error}".Trim());

            StatusText = summaryLines.Count > 0
                ? $"Cold Alley runtime submit complete: {string.Join("; ", summaryLines)}"
                : "Cold Alley runtime submit complete. Check the game/profile after Xbox sync.";

            _snackbarService.Show("Cold Alley Submit Complete", StatusText, NoticeAppearance.Success, new NoticeIcon(NoticeSymbol.Checkmark24), TimeSpan.FromSeconds(8));
        }
        finally
        {
            IsRuntimeSubmitting = false;
        }
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunProcessAsync(string fileName, string arguments)
    {
        var output = new StringBuilder();
        var error = new StringBuilder();
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not null)
                output.AppendLine(args.Data);
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
                error.AppendLine(args.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        return (process.ExitCode, output.ToString(), error.ToString());
    }

    private static string FindColdAlleySubmitScriptPath()
    {
        var candidates = new List<string>
        {
            Path.Combine(Environment.CurrentDirectory, "AchievementLabs", "tools", "Submit-ColdAlleyAchievements.ps1"),
            Path.Combine(Environment.CurrentDirectory, "tools", "Submit-ColdAlleyAchievements.ps1")
        };

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            candidates.Add(Path.Combine(directory.FullName, "tools", "Submit-ColdAlleyAchievements.ps1"));
            candidates.Add(Path.Combine(directory.FullName, "AchievementLabs", "tools", "Submit-ColdAlleyAchievements.ps1"));
            directory = directory.Parent;
        }

        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("Cold Alley runtime submit helper was not found. Expected AchievementLabs\\tools\\Submit-ColdAlleyAchievements.ps1.");
    }

    private static bool IsColdAlley(IWin8SaveTool tool) =>
        tool.GameKey.Equals("coldalley", StringComparison.OrdinalIgnoreCase);

    private static string Quote(string value) =>
        $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    private IWin8SaveTool? GetSelectedTool()
    {
        return _tools.FirstOrDefault(tool => tool.DisplayName == SelectedGameName) ?? _tools.FirstOrDefault();
    }

    private IWin8SaveTool GetRequiredTool()
    {
        return GetSelectedTool() ?? throw new InvalidOperationException("No Windows 8 save tools are registered.");
    }

    private long GetRequiredXuid()
    {
        if (!long.TryParse(Xuid, out var xuid))
            throw new InvalidOperationException("Enter the numeric XUID for the Windows user save.");

        return xuid;
    }
}
