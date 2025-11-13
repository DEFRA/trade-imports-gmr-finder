using Amazon.SQS;
using Amazon.SQS.Model;

namespace GmrFinder.Consumers;

public abstract class SqsConsumer<TConsumer>(ILogger<TConsumer> logger, IAmazonSQS sqsClient, string queueName)
    : BackgroundService
    where TConsumer : class
{
    protected virtual TimeSpan PollDelay { get; } = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queueUrl = (await sqsClient.GetQueueUrlAsync(queueName, stoppingToken)).QueueUrl;

        while (!stoppingToken.IsCancellationRequested)
        {
            var result = await ReceiveMessages(queueUrl, stoppingToken);
            
            foreach (var message in result?.Messages ?? [])
            {
                try
                {
                    await ProcessMessageAsync(message, stoppingToken);
                    await sqsClient.DeleteMessageAsync(queueUrl, message.ReceiptHandle, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Failed to process message {MessageId} from {QueueName}",
                        message.MessageId,
                        queueName
                    );
                }
            }

            try
            {
                await Task.Delay(PollDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task<ReceiveMessageResponse?> ReceiveMessages(string queueUrl, CancellationToken stoppingToken)
    {
        try
        {
            var request = new ReceiveMessageRequest
            {
                MaxNumberOfMessages = 1,
                MessageAttributeNames = ["All"],
                MessageSystemAttributeNames = ["All"],
                QueueUrl = queueUrl,
                VisibilityTimeout = 60,
            };
            return await sqsClient.ReceiveMessageAsync(request, stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to receive messages from {QueueName}", queueName);
            return null;
        }
    }

    protected abstract Task ProcessMessageAsync(Message message, CancellationToken stoppingToken);
}
