using System.Text.Json;
using Amazon.SQS.Model;
using AutoFixture;
using Defra.TradeImportsDataApi.Domain.Events;
using FluentAssertions;
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

        var customsDeclaration = CustomsDeclarationFixtures.CustomsDeclarationFixture().Create();
        var resourceEvent = CustomsDeclarationFixtures
            .CustomsDeclarationResourceEventFixture(customsDeclaration)
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
