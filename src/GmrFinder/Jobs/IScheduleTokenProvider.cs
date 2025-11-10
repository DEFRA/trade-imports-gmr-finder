namespace GmrFinder.Jobs;

public interface IScheduleTokenProvider
{
    Task<bool> TryGetExecutionTokenAsync(string scheduleName, DateTime scheduleExecutionTime, DateTime currentTime);
}
