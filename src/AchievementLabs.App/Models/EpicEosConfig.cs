namespace AchievementLabs.Models;

public partial class EpicEosConfig : ObservableObject
{
    [ObservableProperty] private string _productId = string.Empty;
    [ObservableProperty] private string _sandboxId = string.Empty;
    [ObservableProperty] private string _deploymentId = string.Empty;
    [ObservableProperty] private string _clientId = string.Empty;
    [ObservableProperty] private string _clientSecret = string.Empty;
    [ObservableProperty] private string _encryptionKey = string.Empty;
    [ObservableProperty] private string _captureFolder = string.Empty;
    [ObservableProperty] private string _status = "Not configured";

    public bool HasMinimumConfig =>
        !string.IsNullOrWhiteSpace(ProductId) &&
        !string.IsNullOrWhiteSpace(SandboxId) &&
        !string.IsNullOrWhiteSpace(DeploymentId) &&
        !string.IsNullOrWhiteSpace(ClientId);
}
