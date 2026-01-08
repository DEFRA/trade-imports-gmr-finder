using System.Net;
using System.Net.Http.Json;
using System.Text;
using AutoFixture;
using Defra.TradeImportsDataApi.Domain.CustomsDeclaration;
using Defra.TradeImportsDataApi.Domain.Events;
using Defra.TradeImportsDataApi.Domain.Ipaffs;
using GmrFinder.Configuration;
using GmrFinder.Endpoints;
using GmrFinder.Processing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using TestFixtures;

namespace GmrFinder.Tests.Endpoints;

public class ConsumerEndpointsTests
{
    private const string DevEndpointUsername = "dev-user";
    private const string DevEndpointPassword = "dev-pass";

    [Fact]
    public async Task PostDataEventsQueue_WhenCustomsDeclaration_InvokesCustomsDeclarationProcessor()
    {
        var (app, client, customsDeclarationProcessor, importPreNotificationProcessor) = await BuildClientAsync();
        await using var _ = app;

        var customsDeclaration = CustomsDeclarationFixtures.CustomsDeclarationFixture().Create();
        var resourceEvent = CustomsDeclarationFixtures
            .CustomsDeclarationResourceEventFixture(customsDeclaration)
            .Create();
        var expectedResourceId = resourceEvent.ResourceId;

        var response = await PostAsync(
            client,
            ResourceEventResourceTypes.CustomsDeclaration,
            CreateBasicAuthHeader(DevEndpointUsername, DevEndpointPassword),
            resourceEvent,
            TestContext.Current.CancellationToken
        );

        response.IsSuccessStatusCode.Should().BeTrue();
        await customsDeclarationProcessor
            .Received(1)
            .ProcessAsync(
                Arg.Is<ResourceEvent<CustomsDeclaration>>(customsDeclarationEvent =>
                    customsDeclarationEvent.ResourceId == expectedResourceId
                ),
                Arg.Any<CancellationToken>()
            );
        await importPreNotificationProcessor
            .DidNotReceive()
            .ProcessAsync(Arg.Any<ResourceEvent<ImportPreNotification>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostDataEventsQueue_WhenImportPreNotification_InvokesImportPreNotificationProcessor()
    {
        var (app, client, customsDeclarationProcessor, importPreNotificationProcessor) = await BuildClientAsync();
        await using var _ = app;

        var importPreNotification = ImportPreNotificationFixtures.ImportPreNotificationFixture().Create();
        var resourceEvent = ImportPreNotificationFixtures
            .ImportPreNotificationResourceEventFixture(importPreNotification)
            .Create();
        var expectedResourceId = resourceEvent.ResourceId;

        var response = await PostAsync(
            client,
            ResourceEventResourceTypes.ImportPreNotification,
            CreateBasicAuthHeader(DevEndpointUsername, DevEndpointPassword),
            resourceEvent,
            TestContext.Current.CancellationToken
        );

        response.IsSuccessStatusCode.Should().BeTrue();
        await importPreNotificationProcessor
            .Received(1)
            .ProcessAsync(
                Arg.Is<ResourceEvent<ImportPreNotification>>(importPreNotificationEvent =>
                    importPreNotificationEvent.ResourceId == expectedResourceId
                ),
                Arg.Any<CancellationToken>()
            );
        await customsDeclarationProcessor
            .DidNotReceive()
            .ProcessAsync(Arg.Any<ResourceEvent<CustomsDeclaration>>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("unknown")]
    public async Task PostDataEventsQueue_WhenResourceTypeMissingOrUnknown_ReturnsBadRequest(string? resourceType)
    {
        var (app, client, customsDeclarationProcessor, importPreNotificationProcessor) = await BuildClientAsync();
        await using var _ = app;

        var response = await PostAsync(
            client,
            resourceType: resourceType,
            authorization: CreateBasicAuthHeader(DevEndpointUsername, DevEndpointPassword),
            payload: new { },
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await customsDeclarationProcessor
            .DidNotReceive()
            .ProcessAsync(Arg.Any<ResourceEvent<CustomsDeclaration>>(), Arg.Any<CancellationToken>());
        await importPreNotificationProcessor
            .DidNotReceive()
            .ProcessAsync(Arg.Any<ResourceEvent<ImportPreNotification>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostDataEventsQueue_WhenPayloadInvalid_ReturnsBadRequest()
    {
        var (app, client, customsDeclarationProcessor, importPreNotificationProcessor) = await BuildClientAsync();
        await using var _ = app;

        using var request = new HttpRequestMessage(HttpMethod.Post, "/consumers/data-events-queue");
        request.Headers.Add("ResourceType", ResourceEventResourceTypes.CustomsDeclaration);
        request.Headers.Add("Authorization", CreateBasicAuthHeader(DevEndpointUsername, DevEndpointPassword));
        request.Content = new StringContent("not-json", Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await customsDeclarationProcessor
            .DidNotReceive()
            .ProcessAsync(Arg.Any<ResourceEvent<CustomsDeclaration>>(), Arg.Any<CancellationToken>());
        await importPreNotificationProcessor
            .DidNotReceive()
            .ProcessAsync(Arg.Any<ResourceEvent<ImportPreNotification>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostDataEventsQueue_WhenAuthMissing_ReturnsUnauthorized()
    {
        var (app, client, customsDeclarationProcessor, importPreNotificationProcessor) = await BuildClientAsync();
        await using var _ = app;

        var response = await PostAsync(
            client,
            ResourceEventResourceTypes.CustomsDeclaration,
            authorization: null,
            payload: new { },
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await customsDeclarationProcessor
            .DidNotReceive()
            .ProcessAsync(Arg.Any<ResourceEvent<CustomsDeclaration>>(), Arg.Any<CancellationToken>());
        await importPreNotificationProcessor
            .DidNotReceive()
            .ProcessAsync(Arg.Any<ResourceEvent<ImportPreNotification>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostDataEventsQueue_WhenAuthInvalid_ReturnsUnauthorized()
    {
        var (app, client, customsDeclarationProcessor, importPreNotificationProcessor) = await BuildClientAsync();
        await using var _ = app;

        var response = await PostAsync(
            client,
            ResourceEventResourceTypes.CustomsDeclaration,
            CreateBasicAuthHeader("bad-user", "bad-pass"),
            payload: new { },
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await customsDeclarationProcessor
            .DidNotReceive()
            .ProcessAsync(Arg.Any<ResourceEvent<CustomsDeclaration>>(), Arg.Any<CancellationToken>());
        await importPreNotificationProcessor
            .DidNotReceive()
            .ProcessAsync(Arg.Any<ResourceEvent<ImportPreNotification>>(), Arg.Any<CancellationToken>());
    }

    private static async Task<(
        WebApplication app,
        HttpClient client,
        ICustomsDeclarationProcessor customsDeclarationProcessor,
        IImportPreNotificationProcessor importPreNotificationProcessor
    )> BuildClientAsync()
    {
        var customsDeclarationProcessor = Substitute.For<ICustomsDeclarationProcessor>();
        var importPreNotificationProcessor = Substitute.For<IImportPreNotificationProcessor>();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(
            Options.Create(
                new FeatureOptions
                {
                    DevEndpointUsername = DevEndpointUsername,
                    DevEndpointPassword = DevEndpointPassword,
                }
            )
        );
        builder.Services.AddSingleton(customsDeclarationProcessor);
        builder.Services.AddSingleton(importPreNotificationProcessor);

        var app = builder.Build();
        app.UseRouting();
        app.MapConsumerEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        return (app, app.GetTestClient(), customsDeclarationProcessor, importPreNotificationProcessor);
    }

    private static async Task<HttpResponseMessage> PostAsync<TPayload>(
        HttpClient client,
        string? resourceType,
        string? authorization,
        TPayload payload,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/consumers/data-events-queue");
        if (!string.IsNullOrWhiteSpace(resourceType))
            request.Headers.Add("ResourceType", resourceType);
        if (!string.IsNullOrWhiteSpace(authorization))
            request.Headers.Add("Authorization", authorization);
        request.Content = JsonContent.Create(payload);

        return await client.SendAsync(request, cancellationToken);
    }

    private static string CreateBasicAuthHeader(string username, string password)
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        return $"Basic {encoded}";
    }
}
