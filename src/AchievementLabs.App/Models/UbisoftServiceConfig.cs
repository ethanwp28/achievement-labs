namespace AchievementLabs.Models;

public partial class UbisoftServiceConfig : ObservableObject
{
    [ObservableProperty] private string _clientId = string.Empty;
    [ObservableProperty] private string _clientSecret = string.Empty;
    [ObservableProperty] private string _sessionTicket = string.Empty;
    [ObservableProperty] private string _spaceId = string.Empty;
    [ObservableProperty] private string _environment = "Production";
    [ObservableProperty] private string _captureFolder = string.Empty;
    [ObservableProperty] private string _status = "Not configured";

    public bool HasMinimumConfig =>
        !string.IsNullOrWhiteSpace(ClientId) ||
        !string.IsNullOrWhiteSpace(SessionTicket);
}
