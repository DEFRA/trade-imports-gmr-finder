using System.Reflection;
using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using AutoFixture;
using Defra.TradeImportsDataApi.Domain.CustomsDeclaration;
using Defra.TradeImportsDataApi.Domain.Events;
using Defra.TradeImportsDataApi.Domain.Ipaffs;
using GmrFinder.Configuration;
using GmrFinder.Consumers;
using GmrFinder.Extensions;
using GmrFinder.Metrics;
using GmrFinder.Processing;
using GmrFinder.Tests.Metrics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TestFixtures;

namespace GmrFinder.Tests.Consumers;

public class DataEventsQueueConsumerTests
{
    private DataEventsQueueConsumer _consumer;
    private readonly Mock<ICustomsDeclarationProcessor> _customsDeclarationProcessor = new();
    private readonly Mock<IImportPreNotificationProcessor> _importPreNotificationProcessor = new();
    private readonly MockMeterFactory _meterFactory = new();

    public DataEventsQueueConsumerTests()
    {
        _consumer = new DataEventsQueueConsumer(
            NullLogger<DataEventsQueueConsumer>.Instance,
            new ConsumerMetrics(_meterFactory.CreateMeter()),
            new Mock<IAmazonSQS>().Object,
            Options.Create(
                new DataEventsQueueConsumerOptions
                {
                    QueueName = "trade_imports_data_upserted_gmr_finder",
                    WaitTimeSeconds = 1,
                }
            ),
            _customsDeclarationProcessor.Object,
            _importPreNotificationProcessor.Object
        );
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenResourceTypeIsCustomsDeclaration_SendsToCustomsDeclarationProcessor()
    {
        var body = JsonSerializer.Serialize(
            CustomsDeclarationFixtures
                .CustomsDeclarationResourceEventFixture(CustomsDeclarationFixtures.CustomsDeclarationFixture().Create())
                .Create()
        );

        var message = new Message
        {
            Body = body,
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                [SqsMessageHeaders.ResourceType] = new()
                {
                    DataType = "String",
                    StringValue = ResourceEventResourceTypes.CustomsDeclaration,
                },
            },
        };

        await InvokeProcessMessageAsync(message, CancellationToken.None);

        _customsDeclarationProcessor.Verify(
            processor =>
                processor.ProcessAsync(It.IsAny<ResourceEvent<CustomsDeclaration>>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenResourceTypeIsImportPreNotification_SendsToImportPreNotificationProcessor()
    {
        var body = JsonSerializer.Serialize(
            ImportPreNotificationFixtures
                .ImportPreNotificationResourceEventFixture(
                    ImportPreNotificationFixtures.ImportPreNotificationFixture().Create()
                )
                .Create()
        );

        var message = new Message
        {
            Body = body,
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                [SqsMessageHeaders.ResourceType] = new()
                {
                    DataType = "String",
                    StringValue = ResourceEventResourceTypes.ImportPreNotification,
                },
            },
        };

        await InvokeProcessMessageAsync(message, CancellationToken.None);

        _importPreNotificationProcessor.Verify(
            processor =>
                processor.ProcessAsync(It.IsAny<ResourceEvent<ImportPreNotification>>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenResourceTypeUnhandled_DoesNothing()
    {
        var message = new Message
        {
            Body = "{}",
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                [SqsMessageHeaders.ResourceType] = new() { DataType = "String", StringValue = "Unknown" },
            },
        };

        await InvokeProcessMessageAsync(message, CancellationToken.None);

        _customsDeclarationProcessor.Verify(
            processor =>
                processor.ProcessAsync(It.IsAny<ResourceEvent<CustomsDeclaration>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );

        _importPreNotificationProcessor.Verify(
            processor =>
                processor.ProcessAsync(It.IsAny<ResourceEvent<ImportPreNotification>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenResourceTypeIsCustomsDeclaration_AndSkipAllMessage_DoesNothing()
    {
        _consumer = new DataEventsQueueConsumer(
            NullLogger<DataEventsQueueConsumer>.Instance,
            new ConsumerMetrics(_meterFactory.CreateMeter()),
            new Mock<IAmazonSQS>().Object,
            Options.Create(
                new DataEventsQueueConsumerOptions
                {
                    QueueName = "trade_imports_data_upserted_gmr_finder",
                    WaitTimeSeconds = 1,
                    SkipAllMessages = true,
                }
            ),
            _customsDeclarationProcessor.Object,
            _importPreNotificationProcessor.Object
        );

        var body = JsonSerializer.Serialize(
            CustomsDeclarationFixtures
                .CustomsDeclarationResourceEventFixture(CustomsDeclarationFixtures.CustomsDeclarationFixture().Create())
                .Create()
        );

        var message = new Message
        {
            Body = body,
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                [SqsMessageHeaders.ResourceType] = new()
                {
                    DataType = "String",
                    StringValue = ResourceEventResourceTypes.CustomsDeclaration,
                },
            },
        };

        await InvokeProcessMessageAsync(message, CancellationToken.None);

        _customsDeclarationProcessor.Verify(
            processor =>
                processor.ProcessAsync(It.IsAny<ResourceEvent<CustomsDeclaration>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );

        _importPreNotificationProcessor.Verify(
            processor =>
                processor.ProcessAsync(It.IsAny<ResourceEvent<ImportPreNotification>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    private Task InvokeProcessMessageAsync(Message message, CancellationToken cancellationToken)
    {
        var method = typeof(DataEventsQueueConsumer).GetMethod(
            "ProcessMessageAsync",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!;

        return (Task)method.Invoke(_consumer, [message, cancellationToken])!;
    }
}
