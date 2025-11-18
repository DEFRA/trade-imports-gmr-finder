using GmrFinder.Data;
using GvmsClient.Contract;

namespace GmrFinder.Polling;

public class PollingItemCompletionService(
    ILogger<PollingItemCompletionService> logger,
    TimeProvider? timeProvider = null
) : IPollingItemCompletionService
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public CompletionResult DetermineCompletion(PollingItem pollingItem, List<Gmr> gmrs)
    {
        if (gmrs.Count > 0 && gmrs.All(g => string.Equals(g.State, "COMPLETED", StringComparison.OrdinalIgnoreCase)))
        {
            var reason = "All GMRs are in COMPLETED state";
            logger.LogInformation("Marking polling item {Mrn} as complete: {Reason}", pollingItem.Id, reason);
            return CompletionResult.Complete(reason);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (now > pollingItem.ExpiryDate)
        {
            var reason = $"Polling item expired on {pollingItem.ExpiryDate:yyyy-MM-dd}";
            logger.LogInformation("Marking polling item {Mrn} as complete: {Reason}", pollingItem.Id, reason);
            return CompletionResult.Complete(reason);
        }

        return CompletionResult.Incomplete();
    }
}
