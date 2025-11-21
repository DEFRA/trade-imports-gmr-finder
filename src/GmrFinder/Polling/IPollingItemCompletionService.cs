using Defra.TradeImportsGmrFinder.GvmsClient.Contract;
using GmrFinder.Data;

namespace GmrFinder.Polling;

public class CompletionResult
{
    private CompletionResult(bool shouldComplete, string? reason)
    {
        ShouldComplete = shouldComplete;
        Reason = reason;
    }

    public bool ShouldComplete { get; }
    public string? Reason { get; }

    public static CompletionResult Complete(string reason) => new(true, reason);

    public static CompletionResult Incomplete() => new(false, null);
}

public interface IPollingItemCompletionService
{
    CompletionResult DetermineCompletion(PollingItem pollingItem, List<Gmr> gmrs);
}
