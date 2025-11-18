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
        var completedGmr = gmrs.FirstOrDefault(g => g.State == "COMPLETED");
        if (completedGmr is not null)
        {
            var reason = $"GMR {completedGmr.GmrId} is in COMPLETED state";
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
