using GmrFinder.Configuration;
using GmrFinder.Polling;
using Microsoft.Extensions.Options;

namespace GmrFinder.Jobs;

public class PollGvmsByMrn(
    ILogger<PollGvmsByMrn> logger,
    IScheduleTokenProvider scheduleTokenProvider,
    IOptions<Dictionary<string, ScheduledJob>> config,
    IPollingService pollingService
) : CronHostedService(logger, scheduleTokenProvider, config.Value[JobName].Cron, JobName)
{
    private const string JobName = "poll_gvms_by_mrn";

    protected override async Task DoWork(CancellationToken cancellationToken)
    {
        logger.LogInformation("Executing {Name}", nameof(PollGvmsByMrn));
        await pollingService.PollItems(cancellationToken);
    }
}
