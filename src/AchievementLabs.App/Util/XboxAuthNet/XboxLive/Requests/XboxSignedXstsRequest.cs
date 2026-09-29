using System;
using System.Net.Http;
using System.Threading.Tasks;
using XboxAuthNet.XboxLive.Crypto;
using XboxAuthNet.XboxLive.Responses;

namespace XboxAuthNet.XboxLive.Requests
{
    public class XboxSignedXstsRequest : AbstractXboxSignedAuthRequest
    {
        protected override string RequestUrl => XboxXstsRequest.XstsAuthorizeUrl;

        public string? UserToken { get; set; }
        public string? RelyingParty { get; set; } = XboxAuthConstants.XboxLiveRelyingParty;
        public string? DeviceToken { get; set; }
        public string? TitleToken { get; set; }
        public string[]? OptionalDisplayClaims { get; set; }

        protected override object BuildBody(object proofKey)
        {
            if (string.IsNullOrEmpty(UserToken))
                throw new InvalidOperationException("UserToken was null");
            if (string.IsNullOrEmpty(RelyingParty))
                throw new InvalidOperationException("RelyingParty was null");

            return new
            {
                RelyingParty = RelyingParty,
                TokenType = "JWT",
                Properties = new
                {
                    SandboxId = "RETAIL",
                    UserTokens = new[] { UserToken },
                    DeviceToken = DeviceToken,
                    TitleToken = TitleToken,
                    OptionalDisplayClaims = OptionalDisplayClaims
                }
            };
        }

        public Task<XboxAuthResponse> Send(HttpClient httpClient, IXboxRequestSigner signer)
        {
            return Send<XboxAuthResponse>(httpClient, signer);
        }
    }
}
