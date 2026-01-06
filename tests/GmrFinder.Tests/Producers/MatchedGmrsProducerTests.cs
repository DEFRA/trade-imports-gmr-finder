using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using AutoFixture;
using Defra.TradeImportsGmrFinder.Domain.Events;
using Defra.TradeImportsGmrFinder.GvmsClient.Contract;
using GmrFinder.Configuration;
using GmrFinder.Producers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace GmrFinder.Tests.Producers;

public class MatchedGmrsProducerTests
{
    private readonly IOptions<MatchedGmrsProducerOptions> _options = Options.Create(
        new MatchedGmrsProducerOptions { TopicArn = "topic_name" }
    );
    private readonly Mock<IAmazonSimpleNotificationService> _mockSns = new();
    private readonly Mock<ILogger<MatchedGmrsProducer>> _mockLogger = new();

    [Fact]
    public async Task PublishMatchedGmrs_WithNoMatchedGmrsProvided_DoesNothing()
    {
        var producer = new MatchedGmrsProducer(_mockLogger.Object, _mockSns.Object, _options);
        await producer.PublishMatchedGmrs([], CancellationToken.None);

        _mockSns.Verify(
            x => x.PublishBatchAsync(It.IsAny<PublishBatchRequest>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task PublishMatchedGmrs_PublishesMatchedGmrsInBatches()
    {
        var fixture = new Fixture();
        var matchedGmrs = fixture.CreateMany<MatchedGmr>(35).ToList();

        var producer = new MatchedGmrsProducer(_mockLogger.Object, _mockSns.Object, _options);

        List<PublishBatchRequest> publishBatchRequests = [];
        _mockSns
            .Setup(x => x.PublishBatchAsync(It.IsAny<PublishBatchRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PublishBatchRequest, CancellationToken>((req, _) => publishBatchRequests.Add(req))
            .ReturnsAsync(new PublishBatchResponse());

        await producer.PublishMatchedGmrs(matchedGmrs, CancellationToken.None);

        publishBatchRequests.Should().HaveCount(4);
        publishBatchRequests.Select(req => req.PublishBatchRequestEntries).SelectMany(x => x).Should().HaveCount(35);

        var entry = publishBatchRequests[0].PublishBatchRequestEntries[0];
        var parsedMessage = JsonSerializer.Deserialize<MatchedGmr>(entry.Message);

        parsedMessage.Should().BeEquivalentTo(matchedGmrs[0]);

        var expectedId = matchedGmrs[0].Mrn + "-" + matchedGmrs[0].Gmr.GmrId;
        entry.Id.Should().BeEquivalentTo(expectedId);
    }

    [Fact]
    public async Task PublishMatchedGmrs_WithMatchedGmrs_LogsPublishingMessage()
    {
        var matchedGmrs = new List<MatchedGmr>
        {
            new()
            {
                Mrn = "MRN1",
                Gmr = new Gmr
                {
                    GmrId = "GMR1",
                    HaulierEori = "GB111",
                    State = "COMPLETED",
                    UpdatedDateTime = DateTime.UtcNow.ToString("O"),
                    Direction = "Inbound",
                },
            },
            new()
            {
                Mrn = "MRN2",
                Gmr = new Gmr
                {
                    GmrId = "GMR2",
                    HaulierEori = "GB222",
                    State = "COMPLETED",
                    UpdatedDateTime = DateTime.UtcNow.ToString("O"),
                    Direction = "Inbound",
                },
            },
        };

        var producer = new MatchedGmrsProducer(_mockLogger.Object, _mockSns.Object, _options);

        _mockSns
            .Setup(x => x.PublishBatchAsync(It.IsAny<PublishBatchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublishBatchResponse());

        await producer.PublishMatchedGmrs(matchedGmrs, CancellationToken.None);

        var expectedPairs = string.Join(",", matchedGmrs.Select(m => $"{m.Mrn}:{m.Gmr.GmrId}"));
        var expectedMessage = $"Publishing matched MRN:GMRs: {expectedPairs}";
        _mockLogger.Verify(
            logger =>
                logger.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) => state.ToString()!.Equals(expectedMessage)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
    }
}
