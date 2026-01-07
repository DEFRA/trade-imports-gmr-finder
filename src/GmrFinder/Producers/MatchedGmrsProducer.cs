using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Defra.TradeImportsGmrFinder.Domain.Events;
using GmrFinder.Configuration;
using Microsoft.Extensions.Options;

namespace GmrFinder.Producers;

public class MatchedGmrsProducer(
    ILogger<MatchedGmrsProducer> logger,
    IAmazonSimpleNotificationService snsClient,
    IOptions<MatchedGmrsProducerOptions> options
) : IMatchedGmrsProducer
{
    private readonly MatchedGmrsProducerOptions _options = options.Value;
    private const int AwsConstrainedBatchSize = 10;

    public async Task PublishMatchedGmrs(List<MatchedGmr> matchedRecords, CancellationToken cancellationToken)
    {
        var batchRequests = matchedRecords
            .Select(matchedGmr => new PublishBatchRequestEntry
            {
                Id = matchedGmr.GetIdentifier,
                Message = JsonSerializer.Serialize(matchedGmr),
            })
            .Chunk(AwsConstrainedBatchSize)
            .Select(batch => new PublishBatchRequest
            {
                TopicArn = _options.TopicArn,
                PublishBatchRequestEntries = batch.ToList(),
            })
            .ToList();

        if (batchRequests.Count == 0)
        {
            logger.LogInformation("No matched GMRs to publish");
            return;
        }

        var matchedPairs = string.Join(",", matchedRecords.Select(m => $"{m.Mrn}:{m.Gmr.GmrId}"));
        logger.LogInformation("Publishing matched MRN:GMRs: {MatchedPairs}", matchedPairs);

        var tasks = batchRequests.Select(async request =>
        {
            logger.LogInformation("Publishing batch of {Count} matched GMRs", request.PublishBatchRequestEntries.Count);
            var response = await snsClient.PublishBatchAsync(request, cancellationToken);
            if (response.Failed.Count > 0)
            {
                logger.LogWarning(
                    "Failed to publish {Count} matched GMRs: {Failures}",
                    response.Failed.Count,
                    string.Join(",", response.Failed.Select(f => f.Id))
                );
            }
        });

        await Task.WhenAll(tasks);
    }
}
