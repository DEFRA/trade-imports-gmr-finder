using System.Net;
using GvmsClient.Client;
using GvmsClient.Contract.Requests;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace GvmsClient.Tests.Client;

public sealed class GvmsApiClientTests : IDisposable
{
    private readonly WireMockServer _mockHttpServer = WireMockServer.Start();

    private GvmsApiClient CreateClient()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var gvmsApiSettings = new GvmsApiOptions
        {
            BaseUri = _mockHttpServer.Urls[0],
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
        };

        _mockHttpServer.Reset();

        return new GvmsApiClient(
            new HttpClient { BaseAddress = new Uri(gvmsApiSettings.BaseUri) },
            Options.Create(gvmsApiSettings),
            cache
        );
    }

    [Fact]
    public async Task ShouldSearchByMrnsResults()
    {
        var accessToken = AccessTokenHelper.Create();
        var client = CreateClient();

        _mockHttpServer.MockTokenEndpoint(accessToken);

        _mockHttpServer.MockPostEndpoint(
            "/customs/goods-movement-system-search/gmrs-for-declaration-ids",
            new MrnSearchRequest("SOME_MRN", "SOME_OTHER_MRN"),
            ReadEmbeddedResource("GvmsApiClientTests.ShouldSearchByMrnsResults.response.json"),
            accessToken
        );

        var result = await client.SearchForGmrs(
            new MrnSearchRequest("SOME_MRN", "SOME_OTHER_MRN"),
            CancellationToken.None
        );
        Assert.NotNull(result.StringResult);
        Assert.NotNull(result.Result);
        await Verify(result.Result);
    }

    [Fact]
    public async Task ShouldThrowHttpRequestExceptionSearchByMrnsResults()
    {
        var accessToken = AccessTokenHelper.Create();
        var client = CreateClient();

        _mockHttpServer.MockTokenEndpoint(accessToken);

        _mockHttpServer
            .Given(
                Request
                    .Create()
                    .WithPath("/customs/goods-movement-system-search/gmrs-for-declaration-ids")
                    .WithHeader("Accept", "application/vnd.hmrc.1.0+json")
                    .WithHeader("Authorization", $"Bearer {accessToken.AccessToken}")
                    .WithBodyAsJson(new MrnSearchRequest("").AsJsonString())
                    .UsingPost()
            )
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(HttpStatusCode.BadRequest)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("{}")
            );

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.SearchForGmrs(new MrnSearchRequest(""), CancellationToken.None)
        );
        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
    }

    [Fact]
    public async Task ShouldSearchByVrnsResults()
    {
        var accessToken = AccessTokenHelper.Create();
        var client = CreateClient();

        _mockHttpServer.MockTokenEndpoint(accessToken);
        _mockHttpServer.MockPostEndpoint(
            "/customs/goods-movement-system-search/gmrs-for-vrns",
            new VrnSearchRequest("SOME_VRN", "SOME_OTHER_VRN"),
            ReadEmbeddedResource("GvmsApiClientTests.ShouldSearchByVrnsResults.response.json"),
            accessToken
        );

        var result = await client.SearchForGmrs(
            new VrnSearchRequest("SOME_VRN", "SOME_OTHER_VRN"),
            CancellationToken.None
        );
        Assert.NotNull(result.StringResult);
        Assert.NotNull(result.Result);
        await Verify(result.Result);
    }

    [Fact]
    public async Task ShouldSearchByTrnResults()
    {
        var accessToken = AccessTokenHelper.Create();
        var client = CreateClient();
        _mockHttpServer.MockTokenEndpoint(accessToken);

        _mockHttpServer.MockPostEndpoint(
            "/customs/goods-movement-system-search/gmrs",
            new TrnSearchRequest("SOME_TRN", "SOME_OTHER_TRN"),
            ReadEmbeddedResource("GvmsApiClientTests.ShouldSearchByTrnsResults.response.json"),
            accessToken
        );

        var result = await client.SearchForGmrs(
            new TrnSearchRequest("SOME_TRN", "SOME_OTHER_TRN"),
            CancellationToken.None
        );
        Assert.NotNull(result.StringResult);
        Assert.NotNull(result.Result);
        await Verify(result.Result);
    }

    [Fact]
    public async Task ShouldHoldGmr()
    {
        var accessToken = AccessTokenHelper.Create();
        var client = CreateClient();

        _mockHttpServer.MockTokenEndpoint(accessToken);
        _mockHttpServer.MockHoldEndpoint(
            "/customs/goods-movement-system-search/defra-holds/GMRA44448881",
            new HoldGmrRequest(true),
            accessToken
        );

        await client.HoldGmr("GMRA44448881", true, CancellationToken.None);
    }

    [Fact]
    public async Task ShouldSerialiseMrnSearchRequest()
    {
        await Verify(new MrnSearchRequest("MRN1", "MRN2").AsJsonString());
    }

    [Fact]
    public async Task ShouldSerialiseTrnSearchRequest()
    {
        await Verify(new TrnSearchRequest("TRN1", "TRN2").AsJsonString());
    }

    [Fact]
    public async Task ShouldSerialiseVrnSearchRequest()
    {
        await Verify(new VrnSearchRequest("VRN1", "VRN2").AsJsonString());
    }

    private static Type Anchor => typeof(WireMockGvmsExtensions);

    private static string ReadEmbeddedResource(string resourceName)
    {
        var fullResourceName = $"{typeof(GvmsApiClientTests).Namespace}.{resourceName}";
        using var stream = Anchor.Assembly.GetManifestResourceStream(fullResourceName);

        if (stream is null)
            throw new InvalidOperationException($"Unable to find embedded resource {fullResourceName}");

        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd();
    }

    public void Dispose() => _mockHttpServer.Dispose();
}
