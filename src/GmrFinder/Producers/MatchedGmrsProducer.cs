using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Domain.Events;
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

        var matchedMrns = matchedRecords.Select(m => m.Mrn);
        var matchedGmrs = matchedRecords.Select(m => m.Gmr.GmrId);

        logger.LogInformation(
            "Publishing matched MRNs: {Mrns} to GMRs: {Gmrs}",
            string.Join(",", matchedMrns),
            string.Join(",", matchedGmrs)
        );

        var tasks = batchRequests.Select(async request =>
        {
            logger.LogInformation("Publishing batch of {Count} matched GMRs", request.PublishBatchRequestEntries.Count);
            var response = await snsClient.PublishBatchAsync(request, cancellationToken);
            if (response.Failed.Count > 0)
            {
                // TO-DO: Identify if we need to retry here
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
