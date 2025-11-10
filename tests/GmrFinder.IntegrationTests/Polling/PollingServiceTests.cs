using System.Text.Json;
using Amazon.SQS.Model;
using AutoFixture;
using Defra.TradeImportsDataApi.Domain.Events;
using FluentAssertions;
using GmrFinder.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TestFixtures;

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

                return item is { LastPolled: not null, Gmrs.Keys.Count: > 0 };
            },
            TestContext.Current.CancellationToken
        );

        pollingItemUpdated.Should().BeTrue();
    }
}