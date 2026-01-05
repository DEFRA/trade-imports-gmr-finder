using System.Diagnostics.Metrics;
using AutoFixture;
using GmrFinder.Metrics;
using GmrFinder.Polling;
using GmrFinder.Processing;
using GmrFinder.Utils.Validators;
using Microsoft.Extensions.Logging;
using Moq;
using TestFixtures;

namespace GmrFinder.Tests.Processing;

public class ImportPreNotificationProcessorTests
{
    private readonly Mock<ILogger<ImportPreNotificationProcessor>> _logger = new();
    private readonly Mock<IMeterFactory> _meterFactory = new();
    private readonly Mock<IPollingService> _pollingService = new();
    private readonly ImportPreNotificationProcessor _processor;
    private readonly Mock<IStringValidators> _stringValidators = new();

    public ImportPreNotificationProcessorTests()
    {
        _pollingService
            .Setup(service => service.Process(It.IsAny<PollingRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _stringValidators.Setup(x => x.IsValidMrn(It.IsAny<string>())).Returns(true);

        _meterFactory.Setup(x => x.Create(It.IsAny<MeterOptions>())).Returns(new Meter("test"));

        _processor = new ImportPreNotificationProcessor(
            _logger.Object,
            _pollingService.Object,
            _stringValidators.Object,
            new PollingMetrics(_meterFactory.Object)
        );
    }

    [Fact]
    public async Task ProcessAsync_WhenImportPreNotificationIsNotTransit_SkipsProcessing()
    {
        var importPreNotification = ImportPreNotificationFixtures.ImportPreNotificationFixture().Create();
        var resourceEvent = ImportPreNotificationFixtures
            .ImportPreNotificationResourceEventFixture(importPreNotification)
            .Create();

        await _processor.ProcessAsync(resourceEvent, CancellationToken.None);

        _pollingService.Verify(
            service => service.Process(It.IsAny<PollingRequest>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ProcessAsync_WhenImportPreNotificationIsNotATransit_SkipsAndLogsMessage()
    {
        var importPreNotification = ImportPreNotificationFixtures.ImportPreNotificationFixture().Create();
        var resourceEvent = ImportPreNotificationFixtures
            .ImportPreNotificationResourceEventFixture(importPreNotification)
            .Create();

        await _processor.ProcessAsync(resourceEvent, CancellationToken.None);

        var expectedMessage = $"Skipping Ipaffs record {resourceEvent.ResourceId} because it does not have an NCTS MRN";
        _logger.Verify(
            logger =>
                logger.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) => state.ToString()!.Equals(expectedMessage)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task ProcessAsync_WhenNctsMrnIsInvalid_SkipsProcessing()
    {
        var importPreNotification = ImportPreNotificationFixtures.ImportPreNotificationFixture("mrn123").Create();
        var resourceEvent = ImportPreNotificationFixtures
            .ImportPreNotificationResourceEventFixture(importPreNotification)
            .Create();

        _stringValidators.Setup(x => x.IsValidMrn(It.IsAny<string>())).Returns(false);

        await _processor.ProcessAsync(resourceEvent, CancellationToken.None);

        _pollingService.Verify(
            service => service.Process(It.IsAny<PollingRequest>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ProcessAsync_WhenNctsMrnIsInvalid_LogsInvalidMrnMessage()
    {
        const string mrn = "mrn123";
        var importPreNotification = ImportPreNotificationFixtures.ImportPreNotificationFixture(mrn).Create();
        var resourceEvent = ImportPreNotificationFixtures
            .ImportPreNotificationResourceEventFixture(importPreNotification)
            .Create();

        _stringValidators.Setup(x => x.IsValidMrn(It.IsAny<string>())).Returns(false);

        await _processor.ProcessAsync(resourceEvent, CancellationToken.None);

        var expectedMessage = $"Skipping Ipaffs record {resourceEvent.ResourceId} due to invalid NCTS MRN: {mrn}";
        _logger.Verify(
            logger =>
                logger.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) => state.ToString()!.Equals(expectedMessage)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task ProcessAsync_WhenImportPreNotificationHasNctsReference_InvokesPolling()
    {
        const string chedReference = "CHEDPP.GB.2025.1053368";
        const string mrn = "25GB6RLA6C8OV8GAR2";

        var importPreNotification = ImportPreNotificationFixtures.ImportPreNotificationFixture(mrn).Create();
        var resourceEvent = ImportPreNotificationFixtures
            .ImportPreNotificationResourceEventFixture(importPreNotification)
            .With(r => r.ResourceId, chedReference)
            .Create();

        await _processor.ProcessAsync(resourceEvent, CancellationToken.None);

        _pollingService.Verify(
            service =>
                service.Process(It.Is<PollingRequest>(request => request.Mrn == mrn), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
}
