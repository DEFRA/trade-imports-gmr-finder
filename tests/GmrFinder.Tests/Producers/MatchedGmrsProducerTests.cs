using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using AutoFixture;
using Domain.Events;
using FluentAssertions;
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
    private readonly ILogger<MatchedGmrsProducer> _mockLogger = Mock.Of<ILogger<MatchedGmrsProducer>>();

    [Fact]
    public async Task PublishMatchedGmrs_WithNoMatchedGmrsProvided_DoesNothing()
    {
        var producer = new MatchedGmrsProducer(_mockLogger, _mockSns.Object, _options);
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

        var producer = new MatchedGmrsProducer(_mockLogger, _mockSns.Object, _options);

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
}
