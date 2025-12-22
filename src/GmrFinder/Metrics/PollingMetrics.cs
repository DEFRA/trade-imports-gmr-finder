using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.Serialization;
using Amazon.CloudWatch.EMF.Model;
using GmrFinder.Polling;

namespace GmrFinder.Metrics;

public class PollingMetrics
{
    public enum ItemSource
    {
        [EnumMember(Value = "CUSTOMS_DECLARATION")]
        CustomsDeclaration,

        [EnumMember(Value = "IMPORT_NOTIFICATION")]
        ImportNotification,
    }

    public const string MrnQueueName = "MRN";
    private readonly Histogram<double> _itemDuration;

    private readonly Counter<long> _itemJoined;
    private readonly Counter<long> _itemLeave;

    public PollingMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MetricsConstants.MetricNames.MeterName);

        _itemJoined = meter.CreateCounter<long>(
            "polling.queue.item.joined",
            nameof(Unit.COUNT),
            "Number of items joining the queue"
        );

        _itemLeave = meter.CreateCounter<long>(
            "polling.queue.item.leave",
            nameof(Unit.COUNT),
            "Number of items leaving the queue"
        );

        _itemDuration = meter.CreateHistogram<double>(
            "polling.queue.item.duration",
            nameof(Unit.SECONDS),
            "Duration the item has been in the queue when it leaves"
        );
    }

    public void RecordItemJoined(
        string queue,
        ItemSource source,
        params ReadOnlySpan<KeyValuePair<string, object?>> tags
    )
    {
        var tagList = new TagList { { "queue", queue }, { "source", source } };

        foreach (var tag in tags)
            tagList.Add(tag);

        _itemJoined.Add(1L, tagList);
    }

    public void RecordItemLeave(
        string queue,
        CompletionResult result,
        params ReadOnlySpan<KeyValuePair<string, object?>> tags
    )
    {
        var tagList = new TagList { { "queue", queue }, { "reason", result.Reason } };

        foreach (var tag in tags)
            tagList.Add(tag);

        _itemLeave.Add(1L, tagList);
        _itemDuration.Record(result.Duration!.Value.TotalSeconds, tagList);
    }
}
