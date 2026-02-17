using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Defra.TradeImportsDataApi.Domain.CustomsDeclaration;
using Defra.TradeImportsDataApi.Domain.Events;
using Defra.TradeImportsDataApi.Domain.Ipaffs;
using GmrFinder.Data;
using GmrFinder.Processing;
using GmrFinder.Security;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace GmrFinder.Endpoints;

public static class EndpointRouteBuilderExtensions
{
    public static void MapDevEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/consumers/data-events-queue", Post).AddEndpointFilter<BasicAuthEndpointFilter>();
        app.MapDelete("/polling-queue/items", DeleteAllPollingItems).AddEndpointFilter<BasicAuthEndpointFilter>();
    }

    [HttpDelete]
    private static async Task<IResult> DeleteAllPollingItems(
        IMongoContext mongo,
        CancellationToken cancellationToken
    )
    {
        var logger = loggerFactory.CreateLogger("DevEndpoint");
        logger.LogWarning("Deleting All PollingItems");
        var deleted = await mongo.PollingItems.DeleteMany(FilterDefinition<PollingItem>.Empty, cancellationToken);
        logger.LogWarning("Deleted {Deleted} PollingItems", deleted);
        return Results.Ok(new { deleted });
    }

    [HttpPost]
    private static async Task<IResult> Post(
        [FromServices] ILoggerFactory loggerFactory,
        [FromServices] ICustomsDeclarationProcessor customsDeclarationProcessor,
        [FromServices] IImportPreNotificationProcessor importPreNotificationProcessor,
        [FromHeader(Name = "ResourceType")] string? resourceType,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken
    )
    {
        var logger = loggerFactory.CreateLogger("DataEventsQueueConsumer");
        if (string.IsNullOrWhiteSpace(resourceType))
        {
            logger.LogWarning("Missing ResourceType header");
            return Results.BadRequest("ResourceType header is required.");
        }

        logger.LogInformation("Received ResourceType: {ResourceType}", resourceType);

        switch (resourceType)
        {
            case ResourceEventResourceTypes.CustomsDeclaration:
            {
                var customsDeclaration = body.Deserialize<ResourceEvent<CustomsDeclaration>>(JsonSerializerOptions.Web);
                if (customsDeclaration is null)
                    return Results.BadRequest("Invalid customs declaration payload.");
                await customsDeclarationProcessor.ProcessAsync(customsDeclaration, cancellationToken);
                return Results.Accepted();
            }

            case ResourceEventResourceTypes.ImportPreNotification:
            {
                var importPreNotification = body.Deserialize<ResourceEvent<ImportPreNotification>>(
                    JsonSerializerOptions.Web
                );
                if (importPreNotification is null)
                    return Results.BadRequest("Invalid import pre-notification payload.");
                await importPreNotificationProcessor.ProcessAsync(importPreNotification, cancellationToken);
                return Results.Accepted();
            }

            default:
                logger.LogDebug(
                    "Received unhandled message with resource type: {ResourceType}, skipping",
                    resourceType
                );
                return Results.BadRequest($"Unsupported resourceType '{resourceType}'.");
        }
    }
}
