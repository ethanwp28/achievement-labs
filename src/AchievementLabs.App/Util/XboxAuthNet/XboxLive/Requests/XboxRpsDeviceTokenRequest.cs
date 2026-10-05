using System;
using System.Net.Http;
using System.Threading.Tasks;
using XboxAuthNet.XboxLive.Crypto;
using XboxAuthNet.XboxLive.Responses;

namespace XboxAuthNet.XboxLive.Requests;

public class XboxRpsDeviceTokenRequest : AbstractXboxSignedAuthRequest
{
    public string? AccessToken { get; set; }
    public string? DeviceVersion { get; set; }

    protected override string RequestUrl => "https://device.auth.xboxlive.com/device/authenticate";
    protected override string ContractVersion => "2";

    protected override object BuildBody(object proofKey)
    {
        if (string.IsNullOrEmpty(AccessToken))
            throw new InvalidOperationException("AccessToken was null");
        if (string.IsNullOrEmpty(DeviceVersion))
            throw new InvalidOperationException("DeviceVersion was null");

        return new
        {
            Properties = new
            {
                AuthMethod = "RPS",
                SiteName = "user.auth.xboxlive.com",
                RpsTicket = XboxAuthConstants.XboxTokenPrefix + AccessToken,
                Version = DeviceVersion,
                ProofKey = proofKey
            },
            RelyingParty = XboxAuthConstants.XboxAuthRelyingParty,
            TokenType = "JWT"
        };
    }

    public Task<XboxAuthResponse> Send(HttpClient httpClient, IXboxRequestSigner signer) =>
        Send<XboxAuthResponse>(httpClient, signer);
}
