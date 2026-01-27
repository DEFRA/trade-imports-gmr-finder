using System.Net;
using Microsoft.Extensions.Caching.Memory;

namespace Defra.TradeImportsGmrFinder.GvmsClient.Client;

public class UnauthorizedTokenRefreshHandler(IMemoryCache memoryCache) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            memoryCache.Remove(GvmsApiClient.TokenCacheKey);

        return response;
    }
}
