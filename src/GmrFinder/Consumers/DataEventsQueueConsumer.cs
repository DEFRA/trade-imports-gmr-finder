using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.TradeImportsDataApi.Domain.CustomsDeclaration;
using Defra.TradeImportsDataApi.Domain.Events;
using Defra.TradeImportsDataApi.Domain.Ipaffs;
using GmrFinder.Configuration;
using GmrFinder.Extensions;
using GmrFinder.Metrics;
using GmrFinder.Processing;
using GmrFinder.Utils;
using Microsoft.Extensions.Options;

namespace GmrFinder.Consumers;

public sealed class DataEventsQueueConsumer(
    ILogger<DataEventsQueueConsumer> logger,
    ConsumerMetrics consumerMetrics,
    IAmazonSQS sqsClient,
    IOptions<DataEventsQueueConsumerOptions> options,
    ICustomsDeclarationProcessor customsDeclarationProcessor,
    IImportPreNotificationProcessor importPreNotificationProcessor
) : SqsConsumer<DataEventsQueueConsumer>(logger, consumerMetrics, sqsClient, options.Value.QueueName)
{
    private static readonly JsonSerializerOptions s_defaultSerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly ILogger<DataEventsQueueConsumer> _logger = logger;

    protected override int WaitTimeSeconds { get; } = options.Value.WaitTimeSeconds;

    protected override async Task ProcessMessageAsync(Message message, CancellationToken stoppingToken)
    {
        if (options.Value.SkipAllMessages)
        {
            _logger.LogDebug("Message skipped because SkipAllMessages is set");
            return;
        }

        var json = MessageDeserializer.Deserialize<JsonElement>(message.Body, message.GetContentEncoding());

        _logger.LogInformation("Message received: {ResourceType}", message.GetResourceType());

        switch (message.GetResourceType())
        {
            case ResourceEventResourceTypes.CustomsDeclaration:
                var customsDeclaration = DeserializeAsync<ResourceEvent<CustomsDeclaration>>(json)!;
                await customsDeclarationProcessor.ProcessAsync(customsDeclaration, stoppingToken);
                break;

            case ResourceEventResourceTypes.ImportPreNotification:
                var importPreNotification = DeserializeAsync<ResourceEvent<ImportPreNotification>>(json)!;
                await importPreNotificationProcessor.ProcessAsync(importPreNotification, stoppingToken);
                break;

            default:
                _logger.LogDebug(
                    "Received unhandled message with resource type: {ResourceType}, skipping",
                    message.GetResourceType()
                );
                return;
        }
    }

    private T? DeserializeAsync<T>(JsonElement json)
    {
        try
        {
            return json.Deserialize<T>(s_defaultSerializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Failed to deserialise JSON to {Type}: {Json}", typeof(T).FullName, json.GetRawText());
            throw new JsonException($"Failed to deserialise JSON to {typeof(T).FullName}.", ex);
        }
    }
}
