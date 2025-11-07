using Cronos;

namespace GmrFinder.Jobs;

public abstract class CronHostedService(
    ILogger<CronHostedService> logger,
    IScheduleTokenProvider scheduleTokenProvider,
    string cronExpression,
    string scheduleName,
    TimeProvider? timeProvider = null
) : IHostedService
{
    private readonly CronExpression _schedule = CronExpression.Parse(cronExpression, CronFormat.IncludeSeconds);
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private Task? _executingTask;
    private CancellationTokenSource? _stoppingCts;

    public virtual Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Hosted Service is starting.");

        _stoppingCts = new CancellationTokenSource();
        _executingTask = ExecuteAsync(_stoppingCts.Token);

        return _executingTask.IsCompleted ? _executingTask : Task.CompletedTask;
    }

    public virtual async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Service is stopping.");

        if (_executingTask == null)
            return;

        try
        {
            _stoppingCts?.CancelAsync();
        }
        finally
        {
            await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken));
            _stoppingCts?.Dispose();
        }
    }

    protected abstract Task DoWork();

    private async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = _timeProvider.GetUtcNow();
            var nextOccurrence = _schedule.GetNextOccurrence(now.UtcDateTime, TimeZoneInfo.Utc);

            if (!nextOccurrence.HasValue)
            {
                logger.LogWarning("Cron schedule will not fire again. Stopping.");
                break;
            }

            var delay = nextOccurrence.Value - now;

            if (delay <= TimeSpan.Zero)
            {
                logger.LogDebug("Missed next schedule. Checking for next occurrence immediately.");
                continue;
            }

            try
            {
                logger.LogInformation("Next scheduled run at: {NextRunTimeUtc}", nextOccurrence.Value);
                await Task.Delay(delay, _timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (stoppingToken.IsCancellationRequested)
                break;

            try
            {
                var token = await scheduleTokenProvider.TryGetExecutionTokenAsync(
                    scheduleName,
                    nextOccurrence.Value,
                    _timeProvider.GetUtcNow().DateTime
                );
                if (token)
                {
                    logger.LogInformation("Execution token acquired - Executing.");
                    await DoWork();
                }
                else
                {
                    logger.LogInformation("Execution token not acquired - Continuing.");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "An error occurred executing the scheduled.");
            }
        }

        logger.LogInformation("Service is exiting.");
    }
}
