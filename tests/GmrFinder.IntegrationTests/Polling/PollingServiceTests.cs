using System.Text.Json;
using Amazon.SQS.Model;
using AutoFixture;
using Defra.TradeImportsDataApi.Domain.Events;
using Domain.Events;
using FluentAssertions;
using GmrFinder.Configuration;
using GmrFinder.IntegrationTests.TestExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TestFixtures;
using Xunit.Sdk;

namespace GmrFinder.IntegrationTests.Polling;

public class PollingServiceTests : IntegrationTestBase
{
    [Fact]
    public async Task WhenPollingServicePolls_ShouldUpdateItems()
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

        var pollingItemUpdated = await AsyncWaiter.WaitForAsync(
            async () =>
            {
                var item = await Mongo.PollingItems.FindOne(
                    p => p.Id == expectedMrn,
                    TestContext.Current.CancellationToken
                );

                return item is { LastPolled: not null, Gmrs.Keys.Count: > 0 } ? item : null;
            },
            TestContext.Current.CancellationToken
        );

        pollingItemUpdated.Should().NotBeNull();
    }

    [Fact]
    public async Task WhenPollingServicePolls_ShouldPublishMatchedGmrs()
    {
        var consumerConfig = ServiceProvider.GetRequiredService<IOptions<DataEventsQueueConsumerOptions>>().Value;
        var (sqsClient, queueUrl) = await GetSqsClient(consumerConfig.QueueName);
        var (publishedSqsClient, publishedQueueUrl) = await GetSqsClient(PublishMessageQueueName);

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

        var matchedGmrsPublished = await AsyncWaiter.WaitForAsync(
            async () =>
            {
                var messages = await publishedSqsClient.ReceiveAndDeleteMessages(publishedQueueUrl);

                if (messages.Messages.Count == 0)
                {
                    return null;
                }

                var matchingMessage = messages.Messages.Find(m =>
                {
                    var matchedGmr = JsonSerializer.Deserialize<MatchedGmr>(m.Body);
                    return matchedGmr?.Mrn == expectedMrn;
                });

                return matchingMessage;
            },
            TestContext.Current.CancellationToken
        );

        matchedGmrsPublished.Should().NotBeNull();
    }
}
