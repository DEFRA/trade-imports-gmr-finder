using System.Diagnostics;
using System.Diagnostics.Metrics;
using Amazon.CloudWatch.EMF.Model;

namespace GmrFinder.Metrics;

public class GvmsApiMetrics
{
    private readonly Histogram<double> _requestDuration;

    public GvmsApiMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MetricsConstants.MetricNames.MeterName);

        _requestDuration = meter.CreateHistogram<double>(
            "gvms.api.request.duration",
            nameof(Unit.MILLISECONDS),
            "Duration of GVMS API requests"
        );
    }

    public void RecordRequestDuration(string endpoint, bool success, TimeSpan duration, string? errorType = null)
    {
        var tagList = new TagList { { "endpoint", endpoint }, { "success", success } };

        if (errorType is not null)
        {
            tagList.Add("error_type", errorType);
        }

        _requestDuration.Record(duration.TotalMilliseconds, tagList);
    }
}
