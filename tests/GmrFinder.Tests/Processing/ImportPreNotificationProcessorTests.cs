using AutoFixture;
using GmrFinder.Polling;
using GmrFinder.Processing;
using Microsoft.Extensions.Logging;
using Moq;
using TestFixtures;

namespace GmrFinder.Tests.Processing;

public class ImportPreNotificationProcessorTests
{
    private readonly Mock<ILogger<ImportPreNotificationProcessor>> _logger = new();
    private readonly Mock<IPollingService> _pollingService = new();
    private readonly ImportPreNotificationProcessor _processor;

    public ImportPreNotificationProcessorTests()
    {
        _pollingService
            .Setup(service => service.Process(It.IsAny<PollingRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _processor = new ImportPreNotificationProcessor(_logger.Object, _pollingService.Object);
    }

    [Fact]
    public async Task ProcessAsync_WhenImportPreNotificationIsNotTransit_SkipsPolling()
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
    public async Task ProcessAsync_WhenImportPreNotificationHasNctsReference_InvokesPolling()
    {
        const string chedReference = "CHEDPP.GB.2025.1053368";
        const string mrn = "mrn123";

        var importPreNotification = ImportPreNotificationFixtures.ImportPreNotificationFixture(mrn).Create();
        var resourceEvent = ImportPreNotificationFixtures
            .ImportPreNotificationResourceEventFixture(importPreNotification)
            .With(r => r.ResourceId, chedReference)
            .Create();

        await _processor.ProcessAsync(resourceEvent, CancellationToken.None);

        _pollingService.Verify(
            service =>
                service.Process(
                    It.Is<PollingRequest>(request =>
                        request.Mrn == mrn && request.ChedReferences.SetEquals(new[] { chedReference })
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }
}
