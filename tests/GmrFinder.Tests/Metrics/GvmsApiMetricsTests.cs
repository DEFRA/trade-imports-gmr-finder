using System.Diagnostics.Metrics;
using GmrFinder.Metrics;
using Microsoft.Extensions.DependencyInjection;

namespace GmrFinder.Tests.Metrics;

public class GvmsApiMetricsTests
{
    private readonly GvmsApiMetrics _gvmsApiMetrics;

    public GvmsApiMetricsTests()
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        var serviceProvider = services.BuildServiceProvider();
        var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
        _gvmsApiMetrics = new GvmsApiMetrics(meterFactory);
    }

    [Fact]
    public void RecordRequestDuration_ShouldRecordHistogram_WithSuccessTag()
    {
        const string expectedEndpoint = "SearchForGmrs_Mrn";
        const bool expectedSuccess = true;
        var expectedDuration = TimeSpan.FromMilliseconds(250);
        var measurements = new List<CollectedMeasurement<double>>();

        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (
                instrument.Meter.Name == MetricsConstants.MetricNames.MeterName
                && instrument.Name == "gvms.api.request.duration"
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

        _gvmsApiMetrics.RecordRequestDuration(expectedEndpoint, expectedSuccess, expectedDuration);

        measurements.Should().HaveCount(1);
        var measurement = measurements[0];
        measurement.Value.Should().Be(250);
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("endpoint", expectedEndpoint));
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("success", expectedSuccess));
        measurement.Tags.Should().NotContain(tag => tag.Key == "error_type");
    }

    [Fact]
    public void RecordRequestDuration_ShouldRecordHistogram_WithFailureAndErrorType()
    {
        const string expectedEndpoint = "SearchForGmrs_Vrn";
        const bool expectedSuccess = false;
        const string expectedErrorType = "HttpRequestException";
        var expectedDuration = TimeSpan.FromMilliseconds(150);
        var measurements = new List<CollectedMeasurement<double>>();

        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (
                instrument.Meter.Name == MetricsConstants.MetricNames.MeterName
                && instrument.Name == "gvms.api.request.duration"
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

        _gvmsApiMetrics.RecordRequestDuration(expectedEndpoint, expectedSuccess, expectedDuration, expectedErrorType);

        measurements.Should().HaveCount(1);
        var measurement = measurements[0];
        measurement.Value.Should().Be(150);
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("endpoint", expectedEndpoint));
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("success", expectedSuccess));
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("error_type", expectedErrorType));
    }

    private record CollectedMeasurement<T>(T Value, KeyValuePair<string, object?>[] Tags)
        where T : struct;
}
