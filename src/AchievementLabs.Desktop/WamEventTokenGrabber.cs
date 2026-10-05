using Windows.Security.Authentication.Web.Core;
using Windows.Security.Credentials;
using XboxAuthNet.XboxLive;
using XboxAuthNet.XboxLive.Requests;

namespace AchievementLabs.Desktop;

public static class WamEventTokenGrabber
{
    public static async Task<string> GrabAsync(CancellationToken cancellationToken)
    {
        var provider = await WebAuthenticationCoreManager.FindAccountProviderAsync("https://login.live.com", "consumers");
        if (provider == null)
            throw new InvalidOperationException("Windows could not find a Microsoft account provider. Sign in to the Xbox app and try again.");

        var found = await WebAuthenticationCoreManager.FindAllAccountsAsync(provider, XboxGameTitles.XboxAppPC);
        var accounts = found?.Accounts?.ToList() ?? [];
        if (accounts.Count == 0)
            throw new InvalidOperationException("No Microsoft account is signed in for the Xbox app. Open the Xbox app, sign in, and try again.");

        Exception? last = null;
        foreach (var account in accounts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var ticket = await RequestMicrosoftTicketAsync(provider, account);
                return await ExchangeEventsTokenAsync(ticket, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                last = ex;
            }
        }

        throw last ?? new InvalidOperationException("Windows did not return an Xbox events token. Sign in to the Xbox app and try again.");
    }

    private static async Task<string> RequestMicrosoftTicketAsync(WebAccountProvider provider, WebAccount account)
    {
        var request = new WebTokenRequest(provider, XboxAuthConstants.XboxScope, XboxGameTitles.XboxAppPC);
        var result = await WebAuthenticationCoreManager.GetTokenSilentlyAsync(request, account);
        if (result.ResponseStatus == WebTokenRequestStatus.UserInteractionRequired)
            throw new InvalidOperationException("Windows needs you to sign in again. Open the Xbox app, sign in, and try again.");
        if (result.ResponseStatus != WebTokenRequestStatus.Success)
            throw new InvalidOperationException("Windows did not return a Microsoft account token (" + result.ResponseStatus + "). Sign in to the Xbox app and try again.");

        var ticket = result.ResponseData.Select(data => data.Token).FirstOrDefault(token => !string.IsNullOrWhiteSpace(token));
        if (string.IsNullOrWhiteSpace(ticket))
            throw new InvalidOperationException("Windows returned an empty Microsoft account token. Sign in to the Xbox app and try again.");
        return ticket.StartsWith("t=", StringComparison.Ordinal) ? ticket[2..] : ticket;
    }

    private static async Task<string> ExchangeEventsTokenAsync(string microsoftTicket, CancellationToken cancellationToken)
    {
        using var http = new HttpClient();
        var signed = new XboxSignedClient(http);
        var client = new XboxAuthClient(http);
        var version = Environment.OSVersion.Version.ToString();
        cancellationToken.ThrowIfCancellationRequested();
        var device = await signed.RequestRpsDeviceToken(microsoftTicket, version);
        var user = await client.RequestUserToken(new XboxUserTokenRequest
        {
            AccessToken = XboxAuthConstants.XboxTokenPrefix + microsoftTicket,
            ContractVersion = "2"
        });
        var xsts = await client.RequestXsts(new XboxXstsRequest
        {
            UserToken = user.Token ?? throw new InvalidOperationException("Xbox did not return a user token."),
            DeviceToken = device.Token ?? throw new InvalidOperationException("Xbox did not return a device token."),
            RelyingParty = XboxAuthConstants.XboxEventsRelyingParty,
            ContractVersion = "2"
        });
        var userHash = user.XuiClaims?.UserHash;
        if (string.IsNullOrWhiteSpace(userHash) || string.IsNullOrWhiteSpace(xsts.Token))
            throw new InvalidOperationException("Xbox did not return an events token. Sign in to the Xbox app and try again.");
        return $"x:XBL3.0 x={userHash};{xsts.Token}";
    }
}
