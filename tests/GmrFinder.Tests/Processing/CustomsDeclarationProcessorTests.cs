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

public class CustomsDeclarationProcessorTests
{
    private readonly Mock<ILogger<CustomsDeclarationProcessor>> _logger = new();
    private readonly Mock<IPollingService> _pollingService = new();
    private readonly CustomsDeclarationProcessor _processor;
    private readonly Mock<IStringValidators> _stringValidators = new();
    private readonly Mock<IMeterFactory> _meterFactory = new();

    public CustomsDeclarationProcessorTests()
    {
        _pollingService
            .Setup(service => service.Process(It.IsAny<PollingRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _stringValidators.Setup(x => x.IsValidMrn(It.IsAny<string>())).Returns(true);

        _meterFactory.Setup(x => x.Create(It.IsAny<MeterOptions>())).Returns(new Meter("test"));

        _processor = new CustomsDeclarationProcessor(
            _logger.Object,
            _pollingService.Object,
            _stringValidators.Object,
            new PollingMetrics(_meterFactory.Object)
        );
    }

    [Fact]
    public async Task ProcessAsync_WhenTheMrnIsInvalid_SkipsProcessing()
    {
        var customsDeclaration = CustomsDeclarationFixtures.CustomsDeclarationFixture().Create();

        var resourceEvent = CustomsDeclarationFixtures
            .CustomsDeclarationResourceEventFixture(customsDeclaration)
            .Create();

        _stringValidators.Setup(x => x.IsValidMrn(It.IsAny<string>())).Returns(false);

        await _processor.ProcessAsync(resourceEvent, CancellationToken.None);

        _pollingService.Verify(
            service => service.Process(It.IsAny<PollingRequest>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        var expectedMessage = $"Skipping MRN {resourceEvent.ResourceId} because it has an invalid format";
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
    public async Task ProcessAsync_WithNoChedReferences_SkipsProcessing()
    {
        var customsDeclaration = CustomsDeclarationFixtures
            .CustomsDeclarationFixture()
            .With(x => x.ClearanceDecision, CustomsDeclarationFixtures.ClearanceDecisionFixture([]).Create())
            .Create();

        var resourceEvent = CustomsDeclarationFixtures
            .CustomsDeclarationResourceEventFixture(customsDeclaration)
            .Create();

        await _processor.ProcessAsync(resourceEvent, CancellationToken.None);

        _pollingService.Verify(
            service => service.Process(It.IsAny<PollingRequest>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ProcessAsync_WithANonGVMSPort_SkipsProcessing()
    {
        var customsDeclaration = CustomsDeclarationFixtures
            .CustomsDeclarationFixture()
            .With(
                x => x.ClearanceRequest,
                CustomsDeclarationFixtures
                    .ClearanceRequestFixture()
                    .With(x => x.GoodsLocationCode, "MOOMOOMOO")
                    .Create()
            )
            .Create();

        var resourceEvent = CustomsDeclarationFixtures
            .CustomsDeclarationResourceEventFixture(customsDeclaration)
            .Create();

        await _processor.ProcessAsync(resourceEvent, CancellationToken.None);

        _pollingService.Verify(
            service => service.Process(It.IsAny<PollingRequest>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ProcessAsync_WithANullGVMSPort_SkipsProcessing()
    {
        var customsDeclaration = CustomsDeclarationFixtures
            .CustomsDeclarationFixture()
            .With(
                x => x.ClearanceRequest,
                CustomsDeclarationFixtures
                    .ClearanceRequestFixture()
                    .With(x => x.GoodsLocationCode, (string?)null)
                    .Create()
            )
            .Create();

        var resourceEvent = CustomsDeclarationFixtures
            .CustomsDeclarationResourceEventFixture(customsDeclaration)
            .Create();

        await _processor.ProcessAsync(resourceEvent, CancellationToken.None);

        _pollingService.Verify(
            service => service.Process(It.IsAny<PollingRequest>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ProcessAsync_WhenPortOfArrivalIsNotAGVMSPort_LogsAndSkipsProcessing()
    {
        const string portId = "NOTAGVMSPORT";
        var customsDeclaration = CustomsDeclarationFixtures
            .CustomsDeclarationFixture()
            .With(
                x => x.ClearanceRequest,
                CustomsDeclarationFixtures.ClearanceRequestFixture().With(x => x.GoodsLocationCode, portId).Create()
            )
            .Create();

        var resourceEvent = CustomsDeclarationFixtures
            .CustomsDeclarationResourceEventFixture(customsDeclaration)
            .Create();

        await _processor.ProcessAsync(resourceEvent, CancellationToken.None);

        _pollingService.Verify(
            service => service.Process(It.IsAny<PollingRequest>(), It.IsAny<CancellationToken>()),
            Times.Never
        );

        var expectedMessage = $"Skipping MRN {resourceEvent.ResourceId} because the port {portId} is a non-GVMS port";
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
    public async Task ProcessAsync_LogsReceivedCustomsDeclarationMessage()
    {
        var chedReferences = new List<string> { "CHEDPP.GB.2025.1111111", "CHEDA.GB.2025.2222222" };
        var portOfArrival = "POOPOOGVM";

        var customsDeclaration = CustomsDeclarationFixtures
            .CustomsDeclarationFixture()
            .With(
                x => x.ClearanceDecision,
                CustomsDeclarationFixtures.ClearanceDecisionFixture(chedReferences).Create()
            )
            .With(
                x => x.ClearanceRequest,
                CustomsDeclarationFixtures
                    .ClearanceRequestFixture()
                    .With(x => x.GoodsLocationCode, portOfArrival)
                    .Create()
            )
            .Create();

        var resourceEvent = CustomsDeclarationFixtures
            .CustomsDeclarationResourceEventFixture(customsDeclaration)
            .Create();

        await _processor.ProcessAsync(resourceEvent, CancellationToken.None);

        var expectedMessage =
            $"Received customs declaration, MRN: '{resourceEvent.ResourceId}' - CHEDs: '{string.Join(",", chedReferences)}' - Port of Arrival: '{portOfArrival}'";
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
    public async Task ProcessAsync_WhenAllFieldsProvided_LogsSendingToPollingMessage()
    {
        var chedReferences = new List<string> { "CHEDPP.GB.2025.1111111", "CHEDA.GB.2025.2222222" };
        var portOfArrival = "POOPOOGVM";

        var customsDeclaration = CustomsDeclarationFixtures
            .CustomsDeclarationFixture()
            .With(
                x => x.ClearanceDecision,
                CustomsDeclarationFixtures.ClearanceDecisionFixture(chedReferences).Create()
            )
            .With(
                x => x.ClearanceRequest,
                CustomsDeclarationFixtures
                    .ClearanceRequestFixture()
                    .With(x => x.GoodsLocationCode, portOfArrival)
                    .Create()
            )
            .Create();
        var resourceEvent = CustomsDeclarationFixtures
            .CustomsDeclarationResourceEventFixture(customsDeclaration)
            .Create();

        await _processor.ProcessAsync(resourceEvent, CancellationToken.None);

        var expectedMessage = $"Sending new/updated MRN {resourceEvent.ResourceId} to the polling service";
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
    public async Task ProcessAsync_WhenAllFieldsProvided_InvokesPolling()
    {
        var customsDeclaration = CustomsDeclarationFixtures.CustomsDeclarationFixture().Create();
        var chedReferences = customsDeclaration
            .ClearanceDecision?.Results?.Select(x => x.ImportPreNotification!)
            .ToHashSet();

        chedReferences.Should().NotBeEmpty();

        var resourceEvent = CustomsDeclarationFixtures
            .CustomsDeclarationResourceEventFixture(customsDeclaration)
            .Create();

        await _processor.ProcessAsync(resourceEvent, CancellationToken.None);

        _pollingService.Verify(
            service =>
                service.Process(
                    It.Is<PollingRequest>(request => request.Mrn == resourceEvent.ResourceId),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }
}
