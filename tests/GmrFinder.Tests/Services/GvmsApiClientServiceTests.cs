using System.Diagnostics.Metrics;
using Defra.TradeImportsGmrFinder.GvmsClient.Client;
using Defra.TradeImportsGmrFinder.GvmsClient.Contract;
using Defra.TradeImportsGmrFinder.GvmsClient.Contract.Requests;
using GmrFinder.Metrics;
using GmrFinder.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace GmrFinder.Tests.Services;

public class GvmsApiClientServiceTests
{
    private readonly Mock<IGvmsApiClient> _mockClient;
    private readonly GvmsApiClientService _gvmsApiClient;

    public GvmsApiClientServiceTests()
    {
        _mockClient = new Mock<IGvmsApiClient>();

        var services = new ServiceCollection();
        services.AddMetrics();
        var serviceProvider = services.BuildServiceProvider();
        var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();

        var gvmsApiMetrics = new GvmsApiMetrics(meterFactory);
        _gvmsApiClient = new GvmsApiClientService(_mockClient.Object, gvmsApiMetrics);
    }

    [Fact]
    public async Task SearchForGmrs_Mrn_ShouldRecordSuccessMetric()
    {
        var request = new MrnSearchRequest { DeclarationIds = ["MRN1"] };
        var expectedResponse = new HttpResponseContent<GvmsResponse>(new GvmsResponse { Gmrs = [] }, "{}");
        _mockClient.Setup(c => c.SearchForGmrs(request, It.IsAny<CancellationToken>())).ReturnsAsync(expectedResponse);

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

        var result = await _gvmsApiClient.SearchForGmrsByMrn(request, CancellationToken.None);

        result.Should().Be(expectedResponse);
        measurements.Should().HaveCount(1);
        var measurement = measurements[0];
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("endpoint", "SearchForGmrs_Mrn"));
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("success", true));
        measurement.Tags.Should().NotContain(tag => tag.Key == "error_type");
    }

    [Fact]
    public async Task SearchForGmrs_Mrn_ShouldRecordFailureMetric_WhenExceptionThrown()
    {
        var request = new MrnSearchRequest { DeclarationIds = ["MRN1"] };
        var exception = new HttpRequestException("API Error");
        _mockClient.Setup(c => c.SearchForGmrs(request, It.IsAny<CancellationToken>())).ThrowsAsync(exception);

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

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            _gvmsApiClient.SearchForGmrsByMrn(request, CancellationToken.None)
        );

        measurements.Should().HaveCount(1);
        var measurement = measurements[0];
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("endpoint", "SearchForGmrs_Mrn"));
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("success", false));
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("error_type", "HttpRequestException"));
    }

    private record CollectedMeasurement<T>(T Value, KeyValuePair<string, object?>[] Tags)
        where T : struct;
}
