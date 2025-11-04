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

            if (result?.Messages is null || result.Messages.Count == 0)
            {
                await Task.Delay(PollDelay, stoppingToken);
                continue;
            }

            foreach (var message in result.Messages)
            {
                try
                {
                    await ProcessMessageAsync(message, stoppingToken);
                    await sqsClient.DeleteMessageAsync(queueUrl, message.ReceiptHandle, stoppingToken);
                }
                catch (OperationCanceledException)
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
        }
    }

    private async Task<ReceiveMessageResponse?> ReceiveMessages(string queueUrl, CancellationToken stoppingToken)
    {
        try
        {
            return await sqsClient.ReceiveMessageAsync(queueUrl, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to receive messages from {QueueName}", queueName);
            await Task.Delay(PollDelay, stoppingToken);
            return null;
        }
    }

    protected abstract Task ProcessMessageAsync(Message message, CancellationToken stoppingToken);
}
