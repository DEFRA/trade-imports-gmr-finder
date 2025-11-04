using Amazon.SQS;
using Amazon.SQS.Model;
using GmrFinder.Configuration;
using Microsoft.Extensions.Options;

namespace GmrFinder.Consumers;

public sealed class DataEventsQueueConsumer(
    ILogger<DataEventsQueueConsumer> logger,
    IAmazonSQS sqsClient,
    IOptions<DataEventsQueueConsumerOptions> options
) : SqsConsumer<DataEventsQueueConsumer>(logger, sqsClient, options.Value.QueueName)
{
    private readonly ILogger<DataEventsQueueConsumer> _logger = logger;

    protected override Task ProcessMessageAsync(Message message, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Message received: {Body}", message.Body);
        return Task.CompletedTask;
    }
}
