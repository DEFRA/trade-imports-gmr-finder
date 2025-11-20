using System.Net;
using System.Text.Json;
using Defra.TradeImportsGmrFinder.GvmsClient.Client;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Defra.TradeImportsGmrFinder.GvmsClient.Tests.Client;

public static class WireMockGvmsExtensions
{
    public static void MockTokenEndpoint(this WireMockServer server, Token accessToken) =>
        server
            .Given(
                Request
                    .Create()
                    .WithPath("/oauth/token")
                    .WithHeader("Content-Type", "application/x-www-form-urlencoded")
                    .WithBody(
                        new FormUrlEncodedMatcher([
                            "client_secret=test-client-secret",
                            "client_id=test-client-id",
                            "grant_type=client_credentials",
                        ])
                    )
                    .UsingPost()
            )
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(HttpStatusCode.OK)
                    .WithHeader("Content-Type", "application/json")
                    // Wiremock uses newtonsoft internally, so we must serialize the content BEFORE
                    // passing it into the body so it uses the correct property name attributes.
                    .WithBody(JsonSerializer.Serialize(accessToken))
            );

    internal static void MockPostEndpoint(
        this WireMockServer server,
        string expectedPath,
        IHttpRequestContent expectedBody,
        string responseBody,
        Token accessToken
    ) =>
        server
            .Given(
                Request
                    .Create()
                    .WithPath(expectedPath)
                    .WithHeader("Accept", "application/vnd.hmrc.1.0+json")
                    .WithHeader("Authorization", $"Bearer {accessToken.AccessToken}")
                    // Wiremock uses newtonsoft internally, so we must serialize the content BEFORE
                    // passing it into the matcher so it uses the correct property name attributes
                    .WithBodyAsJson(expectedBody.AsJsonString())
                    .UsingPost()
            )
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(HttpStatusCode.OK)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(responseBody)
            );

    internal static void MockHoldEndpoint(
        this WireMockServer server,
        string expectedResource,
        IHttpRequestContent expectedBody,
        Token accessToken
    ) =>
        server
            .Given(
                Request
                    .Create()
                    .WithPath(expectedResource)
                    .WithHeader("Accept", "application/vnd.hmrc.1.0+json")
                    .WithHeader("Authorization", $"Bearer {accessToken.AccessToken}")
                    // Wiremock uses newtonsoft internally, so we must serialize the content BEFORE
                    // passing it into the matcher so it uses the correct property name attributes
                    .WithBodyAsJson(expectedBody.AsJsonString())
                    .UsingPut()
            )
            .RespondWith(
                Response.Create().WithStatusCode(HttpStatusCode.Accepted).WithHeader("Content-Type", "application/json")
            );
}
