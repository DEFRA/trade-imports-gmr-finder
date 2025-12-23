using System.Diagnostics.Metrics;
using GmrFinder.Metrics;
using GmrFinder.Polling;
using Microsoft.Extensions.DependencyInjection;

namespace GmrFinder.Tests.Metrics;

public class PollingMetricsTests
{
    private readonly PollingMetrics _pollingMetrics;

    public PollingMetricsTests()
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        var serviceProvider = services.BuildServiceProvider();
        var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
        _pollingMetrics = new PollingMetrics(meterFactory);
    }

    [Fact]
    public void RecordItemJoined_ShouldRecordCounter()
    {
        const string expectedQueue = "test-queue";
        const PollingMetrics.ItemSource expectedSource = PollingMetrics.ItemSource.CustomsDeclaration;
        var measurements = new List<CollectedMeasurement<long>>();

        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (
                instrument.Meter.Name == MetricsConstants.MetricNames.MeterName
                && instrument.Name == "polling.queue.item.joined"
            )
                listener.EnableMeasurementEvents(instrument);
        };

        meterListener.SetMeasurementEventCallback<long>(
            (_, measurement, tags, _) =>
            {
                measurements.Add(new CollectedMeasurement<long>(measurement, tags.ToArray()));
            }
        );

        meterListener.Start();

        // Act
        _pollingMetrics.RecordItemJoined(expectedQueue, expectedSource);

        // Assert
        measurements.Should().HaveCount(1);
        var measurement = measurements[0];
        measurement.Value.Should().Be(1L);
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("queue", expectedQueue));
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("source", expectedSource));
    }

    [Fact]
    public void RecordItemLeave_ShouldRecordCounterAndHistogram()
    {
        const string expectedQueue = "test-queue";
        var expectedDuration = TimeSpan.FromDays(4);
        var completionResult = CompletionResult.Complete(CompletionReason.Complete, expectedDuration);

        var counterMeasurements = new List<CollectedMeasurement<long>>();
        var histogramMeasurements = new List<CollectedMeasurement<double>>();

        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (
                instrument.Meter.Name == MetricsConstants.MetricNames.MeterName
                && instrument.Name is "polling.queue.item.leave" or "polling.queue.item.duration"
            )
                listener.EnableMeasurementEvents(instrument);
        };

        meterListener.SetMeasurementEventCallback<long>(
            (_, measurement, tags, _) =>
            {
                counterMeasurements.Add(new CollectedMeasurement<long>(measurement, tags.ToArray()));
            }
        );

        meterListener.SetMeasurementEventCallback<double>(
            (_, measurement, tags, _) =>
            {
                histogramMeasurements.Add(new CollectedMeasurement<double>(measurement, tags.ToArray()));
            }
        );

        meterListener.Start();

        _pollingMetrics.RecordItemLeave(expectedQueue, completionResult);

        counterMeasurements.Should().HaveCount(1);
        var counterMeasurement = counterMeasurements[0];
        counterMeasurement.Value.Should().Be(1L);
        counterMeasurement.Tags.Should().Contain(new KeyValuePair<string, object?>("queue", expectedQueue));
        counterMeasurement
            .Tags.Should()
            .Contain(new KeyValuePair<string, object?>("reason", CompletionReason.Complete));

        histogramMeasurements.Should().HaveCount(1);
        var histogramMeasurement = histogramMeasurements[0];
        histogramMeasurement.Value.Should().Be(expectedDuration.TotalSeconds);
        histogramMeasurement.Tags.Should().Contain(new KeyValuePair<string, object?>("queue", expectedQueue));
        histogramMeasurement
            .Tags.Should()
            .Contain(new KeyValuePair<string, object?>("reason", CompletionReason.Complete));
    }

    private record CollectedMeasurement<T>(T Value, KeyValuePair<string, object?>[] Tags)
        where T : struct;
}
