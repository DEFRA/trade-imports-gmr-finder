using System.Security.AccessControl;
using AutoFixture;
using Defra.TradeImportsDataApi.Domain.CustomsDeclaration;
using Defra.TradeImportsDataApi.Domain.Events;
using FluentAssertions;
using GmrFinder.Polling;
using GmrFinder.Processing;
using Microsoft.Extensions.Logging;
using Moq;
using TestFixtures;

namespace GmrFinder.Tests.Processing;

public class CustomsDeclarationProcessorTests
{
    private readonly Mock<ILogger<CustomsDeclarationProcessor>> _logger = new();
    private readonly Mock<IPollingService> _pollingService = new();
    private readonly CustomsDeclarationProcessor _processor;

    public CustomsDeclarationProcessorTests()
    {
        _pollingService
            .Setup(service => service.Process(It.IsAny<PollingRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _processor = new CustomsDeclarationProcessor(_logger.Object, _pollingService.Object);
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
                    It.Is<PollingRequest>(request =>
                        request.Mrn == resourceEvent.ResourceId && request.ChedReferences.SetEquals(chedReferences)
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }
}
