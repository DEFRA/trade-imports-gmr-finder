using System.Diagnostics.Metrics;
using GmrFinder.Metrics;
using Microsoft.Extensions.DependencyInjection;

namespace GmrFinder.Tests.Metrics;

public class ScheduledJobMetricsTests
{
    private readonly ScheduledJobMetrics _scheduledJobMetrics;

    public ScheduledJobMetricsTests()
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        var serviceProvider = services.BuildServiceProvider();
        var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
        _scheduledJobMetrics = new ScheduledJobMetrics(meterFactory);
    }

    [Fact]
    public void RecordExecutionDuration_ShouldRecordHistogram()
    {
        const string expectedJobName = "test-job";
        const bool expectedSuccess = true;
        var expectedDuration = TimeSpan.FromMilliseconds(12345);
        var measurements = new List<CollectedMeasurement<double>>();

        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (
                instrument.Meter.Name == MetricsConstants.MetricNames.MeterName
                && instrument.Name == "jobs.execution.duration"
            )
                listener.EnableMeasurementEvents(instrument);
        };

        meterListener.SetMeasurementEventCallback<double>(
            (_, measurement, tags, _) =>
            {
                measurements.Add(new CollectedMeasurement<double>(measurement, tags.ToArray()));
            }
        );

        meterListener.Start();

        _scheduledJobMetrics.RecordExecutionDuration(expectedJobName, expectedSuccess, expectedDuration);

        measurements.Should().HaveCount(1);
        var measurement = measurements[0];
        measurement.Value.Should().Be(expectedDuration.TotalMilliseconds);
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("jobName", expectedJobName));
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("success", expectedSuccess));
    }

    private record CollectedMeasurement<T>(T Value, KeyValuePair<string, object?>[] Tags)
        where T : struct;
}
