namespace GmrFinder.Jobs;

public record ScheduleToken(string scheduleKey, DateTime scheduleExecutionTime, DateTime currentTime);
