using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Linq;
using System.Text.Json;

namespace XboxAuthNet.XboxLive.Responses
{
    public class XboxAuthResponseHandler
    {
        public async Task<T> HandleResponse<T>(HttpResponseMessage res)
        {
            var resBody = await res.Content.ReadAsStringAsync()
                .ConfigureAwait(false);

            try
            {
                res.EnsureSuccessStatusCode();
                return JsonSerializer.Deserialize<T>(resBody)
                    ?? throw new JsonException();
            }
            catch (Exception ex) when (
                ex is JsonException ||
                ex is HttpRequestException)
            {
                try
                {
                    throw XboxAuthException.FromResponseBody(resBody, (int)res.StatusCode);
                }
                catch (FormatException)
                {
                    try
                    {
                        throw XboxAuthException.FromResponseHeaders(res.Headers, (int)res.StatusCode);
                    }
                    catch (FormatException)
                    {
                        var headerSummary = string.Join("; ", res.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}"));
                        var bodySummary = string.IsNullOrWhiteSpace(resBody)
                            ? "<empty>"
                            : resBody.Length > 1500 ? resBody[..1500] : resBody;
                        throw new XboxAuthException(
                            $"{(int)res.StatusCode}: {res.ReasonPhrase}; body={bodySummary}; headers={headerSummary}",
                            (int)res.StatusCode);
                    }
                }
            }
        }
    }
}
