using System.Net;
using Defra.TradeImportsGmrFinder.GvmsClient.Client;
using Microsoft.Extensions.Caching.Memory;

namespace Defra.TradeImportsGmrFinder.GvmsClient.Tests.Client;

public class UnauthorizedTokenRefreshHandlerTests
{
    private const string TokenCacheKey = "hmrcToken";

    [Fact]
    public async Task ShouldRemoveTokenFromCacheWhen401Unauthorized()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set(TokenCacheKey, "test-token");

        var mockInnerHandler = new MockHttpMessageHandler(HttpStatusCode.Unauthorized);
        var handler = new UnauthorizedTokenRefreshHandler(cache) { InnerHandler = mockInnerHandler };

        var invoker = new HttpMessageInvoker(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/api");

        var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(cache.TryGetValue(TokenCacheKey, out _), "Token should be removed from cache");
    }

    [Fact]
    public async Task ShouldReturnResponseFromInnerHandler()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var mockInnerHandler = new MockHttpMessageHandler(HttpStatusCode.OK, "response-content");
        var handler = new UnauthorizedTokenRefreshHandler(cache) { InnerHandler = mockInnerHandler };

        var invoker = new HttpMessageInvoker(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/api");

        var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("response-content", content);
    }

    private class MockHttpMessageHandler(HttpStatusCode statusCode, string? content = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var response = new HttpResponseMessage(statusCode);
            if (content != null)
                response.Content = new StringContent(content);

            return Task.FromResult(response);
        }
    }
}
