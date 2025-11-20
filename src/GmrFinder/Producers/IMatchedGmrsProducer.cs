using Defra.TradeImportsGmrFinder.Domain.Events;

namespace GmrFinder.Producers;

public interface IMatchedGmrsProducer
{
    Task PublishMatchedGmrs(List<MatchedGmr> matchedRecords, CancellationToken cancellationToken);
}
