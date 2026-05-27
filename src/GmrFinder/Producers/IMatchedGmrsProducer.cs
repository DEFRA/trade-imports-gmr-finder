using Defra.TradeImportsGmrFinder.Domain.Events;

namespace GmrFinder.Producers;

public interface IMatchedGmrsProducer
{
    Task PublishMatchedGmrs(string pollId, List<MatchedGmr> matchedRecords, CancellationToken cancellationToken);
}
