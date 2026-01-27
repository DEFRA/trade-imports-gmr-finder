using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Defra.TradeImportsGmrFinder.GvmsClient.Contract;
using Defra.TradeImportsGmrFinder.GvmsClient.Contract.Requests;
using Defra.TradeImportsGmrFinder.GvmsClient.Contract.Responses;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Defra.TradeImportsGmrFinder.GvmsClient.Client;

public class GvmsApiClient(HttpClient httpClient, IOptions<GvmsApiOptions> gvmsApiSettings, IMemoryCache memoryCache)
    : IGvmsApiClient
{
    /// <summary>
    /// Paths must NOT start with '/' to be treated as relative paths, ensuring they are resolved
    /// from the HttpClient.BaseAddress rather than the root of the domain.
    /// </summary>
    private static class Paths
    {
        public const string Token = "oauth/token";

        public static string HoldGmr(string gmrId) => $"customs/goods-movement-system-search/defra-holds/{gmrId}";

        public const string VrnSearch = "customs/goods-movement-system-search/gmrs-for-vrns";
        public const string MrnSearch = "customs/goods-movement-system-search/gmrs-for-declaration-ids";
        public const string TrnSearch = "customs/goods-movement-system-search/gmrs";
    }

    private readonly GvmsApiOptions _apiSettings = gvmsApiSettings.Value;
    internal const string TokenCacheKey = "hmrcToken";
    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private async Task<string?> GetAccessToken() =>
        await memoryCache.GetOrCreateAsync<string>(
            TokenCacheKey,
            async entry =>
            {
                var token = await GetTokenFromAuthServer();
                entry.SetAbsoluteExpiration(token!.GetExpires());
                return token.AccessToken;
            }
        );

    private async Task<HttpRequestMessage> AddAuthHeaders(HttpRequestMessage request)
    {
        var accessToken = await GetAccessToken();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.hmrc.1.0+json"));
        return request;
    }

    private async Task<Token?> GetTokenFromAuthServer()
    {
        var responseMessage = await httpClient.PostAsync(
            Paths.Token,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    { "grant_type", "client_credentials" },
                    { "client_id", _apiSettings.ClientId },
                    { "client_secret", _apiSettings.ClientSecret },
                }
            )
        );

        responseMessage.EnsureSuccessStatusCode();

        return await responseMessage.Content.ReadFromJsonAsync<Token>();
    }

    private async Task<HttpResponseContent<T>> SendRequest<T>(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        await AddAuthHeaders(request);
        var res = await httpClient.SendAsync(request, cancellationToken);
        res.EnsureSuccessStatusCode();

        var responseString = await res.Content.ReadAsStringAsync(cancellationToken);
        var gvmsResponse = JsonSerializer.Deserialize<T>(responseString, s_jsonSerializerOptions);
        return new HttpResponseContent<T>(gvmsResponse!, responseString);
    }

    private static HttpRequestMessage PostRequest(string path, IHttpRequestContent body) =>
        new(HttpMethod.Post, path) { Content = body.AsJsonContent() };

    public async Task<HttpResponseContent<GvmsResponse>> SearchForGmrs(
        MrnSearchRequest request,
        CancellationToken cancellationToken
    ) => await SendRequest<GvmsResponse>(PostRequest(Paths.MrnSearch, request), cancellationToken);

    public async Task<HttpResponseContent<VrnSearchResponse>> SearchForGmrs(
        VrnSearchRequest request,
        CancellationToken cancellationToken
    ) => await SendRequest<VrnSearchResponse>(PostRequest(Paths.VrnSearch, request), cancellationToken);

    public async Task<HttpResponseContent<TrnSearchResponse>> SearchForGmrs(
        TrnSearchRequest request,
        CancellationToken cancellationToken
    ) => await SendRequest<TrnSearchResponse>(PostRequest(Paths.TrnSearch, request), cancellationToken);

    public async Task HoldGmr(string gmrId, bool holdStatus, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, Paths.HoldGmr(gmrId))
        {
            Content = new HoldGmrRequest(holdStatus).AsJsonContent(),
        };

        await AddAuthHeaders(request);

        var res = await httpClient.SendAsync(request, cancellationToken);
        res.EnsureSuccessStatusCode();
    }
}
