using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.TradeImportsDataApi.Domain.CustomsDeclaration;
using Defra.TradeImportsDataApi.Domain.Events;
using GmrFinder.Configuration;
using GmrFinder.Extensions;
using GmrFinder.Processing;
using GmrFinder.Utils;
using Microsoft.Extensions.Options;

namespace GmrFinder.Consumers;

public sealed class DataEventsQueueConsumer(
    ILogger<DataEventsQueueConsumer> logger,
    IAmazonSQS sqsClient,
    IOptions<DataEventsQueueConsumerOptions> options,
    ICustomsDeclarationProcessor customsDeclarationProcessor
) : SqsConsumer<DataEventsQueueConsumer>(logger, sqsClient, options.Value.QueueName)
{
    private readonly ILogger<DataEventsQueueConsumer> _logger = logger;
    private readonly ICustomsDeclarationProcessor _customsDeclarationProcessor = customsDeclarationProcessor;

    protected override async Task ProcessMessageAsync(Message message, CancellationToken stoppingToken)
    {
        var json = MessageDeserializer.Deserialize<JsonElement>(message.Body, message.GetContentEncoding());

        _logger.LogInformation("Message received: {ResourceType} {Body}", message.GetResourceType(), json);

        switch (message.GetResourceType())
        {
            case ResourceEventResourceTypes.CustomsDeclaration:
                var customsDeclaration = json.Deserialize<ResourceEvent<CustomsDeclaration>>()!;
                await _customsDeclarationProcessor.ProcessAsync(customsDeclaration, stoppingToken);
                break;

            case ResourceEventResourceTypes.ImportPreNotification:
                _logger.LogInformation("Received import pre notification: {Body}", "text");
                break;
        }

        return;
    }
}
