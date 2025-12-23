using System.Runtime.Serialization;
using Defra.TradeImportsGmrFinder.GvmsClient.Contract;
using GmrFinder.Data;

namespace GmrFinder.Polling;

public enum CompletionReason
{
    [EnumMember(Value = "COMPLETE")]
    Complete,

    [EnumMember(Value = "EXPIRED")]
    Expired,
}

public class CompletionResult
{
    private CompletionResult(bool shouldComplete, CompletionReason? reason, TimeSpan? duration)
    {
        ShouldComplete = shouldComplete;
        Reason = reason;
        Duration = duration;
    }

    public bool ShouldComplete { get; }
    public CompletionReason? Reason { get; }

    public TimeSpan? Duration { get; }

    public static CompletionResult Complete(CompletionReason reason, TimeSpan duration)
    {
        return new CompletionResult(true, reason, duration);
    }

    public static CompletionResult Incomplete()
    {
        return new CompletionResult(false, null, null);
    }
}

public interface IPollingItemCompletionService
{
    CompletionResult DetermineCompletion(PollingItem pollingItem, List<Gmr> gmrs);
}
