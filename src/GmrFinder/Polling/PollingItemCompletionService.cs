using Defra.TradeImportsGmrFinder.GvmsClient.Contract;
using GmrFinder.Data;

namespace GmrFinder.Polling;

public class PollingItemCompletionService(
    ILogger<PollingItemCompletionService> logger,
    TimeProvider? timeProvider = null
) : IPollingItemCompletionService
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public CompletionResult DetermineCompletion(PollingItem pollingItem, List<Gmr> gmrs)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var duration = now - pollingItem.Created;
        if (gmrs.Count > 0 && gmrs.All(g => string.Equals(g.State, "COMPLETED", StringComparison.OrdinalIgnoreCase)))
        {
            const string reason = "All GMRs are in COMPLETED state";
            logger.LogInformation("Marking polling item {Mrn} as complete: {Reason}", pollingItem.Id, reason);
            return CompletionResult.Complete(CompletionReason.Complete, duration);
        }

        if (now > pollingItem.ExpiryDate)
        {
            var reason = $"Polling item expired on {pollingItem.ExpiryDate:yyyy-MM-dd}";
            logger.LogInformation("Marking polling item {Mrn} as complete: {Reason}", pollingItem.Id, reason);
            return CompletionResult.Complete(CompletionReason.Expired, duration);
        }

        return CompletionResult.Incomplete();
    }
}
