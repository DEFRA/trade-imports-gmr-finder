using System.Text.Json;
using Defra.TradeImportsGmrFinder.Domain.Events;

namespace GmrFinder.Producers;

public class StubMatchedGmrsProducer(ILogger<StubMatchedGmrsProducer> logger) : IMatchedGmrsProducer
{
    public Task PublishMatchedGmrs(List<MatchedGmr> matchedRecords, CancellationToken cancellationToken)
    {
        if (matchedRecords.Count == 0)
        {
            return Task.CompletedTask;
        }

        var matchedPairs = string.Join(",", matchedRecords.Select(m => $"{m.Mrn}:{m.Gmr.GmrId}"));
        logger.LogInformation(
            "Would publish {Count} matched MRN:GMRs to SNS: {MatchedPairs}",
            matchedRecords.Count,
            matchedPairs
        );

        return Task.CompletedTask;
    }
}
