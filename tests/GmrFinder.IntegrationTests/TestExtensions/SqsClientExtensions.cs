using Amazon.SQS;
using Amazon.SQS.Model;

namespace GmrFinder.IntegrationTests.TestExtensions;

public static class SqsClientExtensions
{
    public static async Task<ReceiveMessageResponse> ReceiveAndDeleteMessages(
        this IAmazonSQS sqsClient,
        string queueUrl
    )
    {
        var messages = await sqsClient.ReceiveMessageAsync(
            new ReceiveMessageRequest { QueueUrl = queueUrl, MaxNumberOfMessages = 10 },
            TestContext.Current.CancellationToken
        );

        foreach (var sqsMessage in messages.Messages)
        {
            await sqsClient.DeleteMessageAsync(
                queueUrl,
                sqsMessage.ReceiptHandle,
                TestContext.Current.CancellationToken
            );
        }

        return messages;
    }
}
