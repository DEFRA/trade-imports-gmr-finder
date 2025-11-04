using FluentAssertions;
using GmrFinder.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GmrFinder.IntegrationTests.Consumers;

public class DataEventsQueueConsumerTests : IntegrationTestBase
{
    [Fact]
    public async Task WhenSqsMessageReceived_ShouldBeProcessed()
    {
        var config = ServiceProvider.GetRequiredService<IOptions<DataEventsQueueConsumerOptions>>().Value;
        var (sqsClient, queueUrl) = await GetSqsClient(config.QueueName);

        await sqsClient.SendMessageAsync(queueUrl, "Hello World", TestContext.Current.CancellationToken);

        var success = await AsyncWaiter.WaitForAsync(
            async () =>
            {
                var numberMessagesOnQueue = await sqsClient.GetQueueAttributesAsync(
                    queueUrl,
                    ["ApproximateNumberOfMessages", "ApproximateNumberOfMessagesNotVisible"]
                );

                return numberMessagesOnQueue.ApproximateNumberOfMessages
                        + numberMessagesOnQueue.ApproximateNumberOfMessages
                    == 0;
            },
            TestContext.Current.CancellationToken
        );

        success.Should().BeTrue();
    }
}
