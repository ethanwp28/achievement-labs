using System.Diagnostics;

namespace AchievementLabs.Models;

public sealed partial class GfwlProcessItem : ObservableObject
{
    [ObservableProperty] private int _processId;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _path = string.Empty;
    [ObservableProperty] private bool _hasXlive;
    [ObservableProperty] private string _status = string.Empty;

    public static GfwlProcessItem FromProcess(Process process, bool hasXlive, string status)
    {
        return new GfwlProcessItem
        {
            ProcessId = process.Id,
            Name = process.ProcessName,
            Path = TryGetPath(process),
            HasXlive = hasXlive,
            Status = status
        };
    }

    private static string TryGetPath(Process process)
    {
        try
        {
            return process.MainModule?.FileName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
