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
    private const int AwsConstrainedBatchSize = 10;
    private readonly MatchedGmrsProducerOptions _options = options.Value;

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

        // Chunking results to avoid 16kb log message limit
        // S2629 : Using string over params to avoid the values being duplicated
        var matchedRecordsChunks = matchedRecords.Chunk(200);
        foreach (var matchedRecordsChunk in matchedRecordsChunks)
        {
            var matchedPairs = string.Join(",", matchedRecordsChunk.Select(m => $"{m.Mrn}:{m.Gmr.GmrId}"));
#pragma warning disable S2629
            logger.LogInformation($"Publishing matched MRN:GMRs: {matchedPairs}");
#pragma warning restore S2629
        }

        var tasks = batchRequests.Select(async request =>
        {
            logger.LogInformation("Publishing batch of {Count} matched GMRs", request.PublishBatchRequestEntries.Count);
            var response = await snsClient.PublishBatchAsync(request, cancellationToken);
            if (response.Failed.Count > 0)
                logger.LogWarning(
                    "Failed to publish {Count} matched GMRs: {Failures}",
                    response.Failed.Count,
                    string.Join(",", response.Failed.Select(f => f.Id))
                );
        });

        await Task.WhenAll(tasks);
    }
}
