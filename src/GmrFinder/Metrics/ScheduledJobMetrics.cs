using System.Diagnostics;
using System.Diagnostics.Metrics;
using Amazon.CloudWatch.EMF.Model;

namespace GmrFinder.Metrics;

public class ScheduledJobMetrics
{
    private readonly Histogram<double> _jobExecutionDuration;

    public ScheduledJobMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MetricsConstants.MetricNames.MeterName);

        _jobExecutionDuration = meter.CreateHistogram<double>(
            "jobs.execution.duration",
            nameof(Unit.MILLISECONDS),
            "Duration of the job execution time"
        );
    }

    public void RecordExecutionDuration(string jobName, bool success, double duration)
    {
        var tagList = new TagList { { "jobName", jobName }, { "success", success } };
        _jobExecutionDuration.Record(duration, tagList);
    }
}
