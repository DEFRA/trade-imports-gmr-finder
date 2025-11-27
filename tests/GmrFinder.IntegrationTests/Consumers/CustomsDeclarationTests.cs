using System.Text.Json;
using Amazon.SQS.Model;
using AutoFixture;
using Defra.TradeImportsDataApi.Domain.Events;
using GmrFinder.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TestFixtures;

namespace GmrFinder.IntegrationTests.Consumers;

public class CustomsDeclarationTests : IntegrationTestBase
{
    [Fact]
    public async Task WhenCustomsDeclarationReceived_ShouldBeProcessed()
    {
        var config = ServiceProvider.GetRequiredService<IOptions<DataEventsQueueConsumerOptions>>().Value;
        var (sqsClient, queueUrl) = await GetSqsClient(config.QueueName);

        var expectedMrn = CustomsDeclarationFixtures.GenerateMrn();

        var customsDeclaration = CustomsDeclarationFixtures.CustomsDeclarationFixture().Create();
        var resourceEvent = CustomsDeclarationFixtures
            .CustomsDeclarationResourceEventFixture(customsDeclaration)
            .With(r => r.ResourceId, expectedMrn)
            .Create();

        var message = new SendMessageRequest
        {
            MessageBody = JsonSerializer.Serialize(resourceEvent),
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                {
                    "ResourceType",
                    new MessageAttributeValue
                    {
                        DataType = "String",
                        StringValue = ResourceEventResourceTypes.CustomsDeclaration,
                    }
                },
            },
            QueueUrl = queueUrl,
        };

        await sqsClient.SendMessageAsync(message, TestContext.Current.CancellationToken);

        var messageConsumed = await AsyncWaiter.WaitForAsync(
            async () =>
            {
                var result = await sqsClient.GetQueueAttributesAsync(
                    queueUrl,
                    ["ApproximateNumberOfMessages", "ApproximateNumberOfMessagesNotVisible"]
                );

                var numberMessagesOnQueue =
                    result.ApproximateNumberOfMessages + result.ApproximateNumberOfMessagesNotVisible;

                return numberMessagesOnQueue == 0 ? (int?)numberMessagesOnQueue : null;
            },
            TestContext.Current.CancellationToken
        );

        messageConsumed.Should().NotBeNull();

        var pollingItemCreated = await AsyncWaiter.WaitForAsync(
            async () =>
            {
                return await Mongo.PollingItems.FindOne(
                    p => p.Id == expectedMrn,
                    TestContext.Current.CancellationToken
                );
            },
            TestContext.Current.CancellationToken
        );

        pollingItemCreated.Should().NotBeNull();
    }
}
