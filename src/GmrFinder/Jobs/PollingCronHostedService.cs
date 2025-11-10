using GmrFinder.Configuration;
using Microsoft.Extensions.Options;

namespace GmrFinder.Jobs;

public class PollingCronHostedService(
    ILogger<PollingCronHostedService> logger,
    IScheduleTokenProvider scheduleTokenProvider,
    IOptions<Dictionary<string, ScheduledJob>> config
) : CronHostedService(logger, scheduleTokenProvider, config.Value[JobName].Cron, JobName)
{
    private const string JobName = "poll_gvms_by_declaration";

    protected override async Task DoWork(CancellationToken cancellationToken)
    {
        logger.LogInformation("executing {Name}", nameof(PollingCronHostedService));
        await Task.CompletedTask;
    }
}
