using FluentAssertions;
using GmrFinder.Data;
using GmrFinder.Polling;
using Defra.TradeImportsGmrFinder.GvmsClient.Contract;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace GmrFinder.Tests.Polling;

public class PollingItemCompletionServiceTests
{
    private readonly Mock<ILogger<PollingItemCompletionService>> _mockLogger = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2025, 11, 13, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public void DetermineCompletion_WithCompletedGmr_ShouldMarkComplete()
    {
        var service = new PollingItemCompletionService(_mockLogger.Object, _timeProvider);
        var pollingItem = new PollingItem
        {
            Id = "mrn123",
            Created = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-5),
            ExpiryDate = _timeProvider.GetUtcNow().UtcDateTime.AddDays(25),
        };

        var gmrs = new List<Gmr>
        {
            new()
            {
                GmrId = "gmr123",
                HaulierEori = "GB123",
                State = "COMPLETED",
                UpdatedDateTime = DateTime.UtcNow.ToString("O"),
                Direction = "Inbound",
            },
        };

        var result = service.DetermineCompletion(pollingItem, gmrs);

        result.ShouldComplete.Should().BeTrue();
        result.Reason.Should().Contain("All GMRs");
        result.Reason.Should().Contain("COMPLETED");
    }

    [Fact]
    public void DetermineCompletion_WithMultipleGmrsOneCompleted_ShouldNotMarkComplete()
    {
        var service = new PollingItemCompletionService(_mockLogger.Object, _timeProvider);
        var pollingItem = new PollingItem
        {
            Id = "mrn123",
            Created = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-5),
            ExpiryDate = _timeProvider.GetUtcNow().UtcDateTime.AddDays(25),
        };

        var gmrs = new List<Gmr>
        {
            new()
            {
                GmrId = "gmr123",
                HaulierEori = "GB123",
                State = "Submitted",
                UpdatedDateTime = DateTime.UtcNow.ToString("O"),
                Direction = "Inbound",
            },
            new()
            {
                GmrId = "gmr456",
                HaulierEori = "GB456",
                State = "COMPLETED",
                UpdatedDateTime = DateTime.UtcNow.ToString("O"),
                Direction = "Inbound",
            },
            new()
            {
                GmrId = "gmr789",
                HaulierEori = "GB789",
                State = "Embarked",
                UpdatedDateTime = DateTime.UtcNow.ToString("O"),
                Direction = "Inbound",
            },
        };

        var result = service.DetermineCompletion(pollingItem, gmrs);

        result.ShouldComplete.Should().BeFalse();
        result.Reason.Should().BeNull();
    }

    [Fact]
    public void DetermineCompletion_WithNoCompletedGmrs_ShouldNotMarkComplete()
    {
        var service = new PollingItemCompletionService(_mockLogger.Object, _timeProvider);
        var pollingItem = new PollingItem
        {
            Id = "mrn123",
            Created = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-5),
            ExpiryDate = _timeProvider.GetUtcNow().UtcDateTime.AddDays(25),
        };

        var gmrs = new List<Gmr>
        {
            new()
            {
                GmrId = "gmr123",
                HaulierEori = "GB123",
                State = "Submitted",
                UpdatedDateTime = DateTime.UtcNow.ToString("O"),
                Direction = "Inbound",
            },
            new()
            {
                GmrId = "gmr456",
                HaulierEori = "GB456",
                State = "Embarked",
                UpdatedDateTime = DateTime.UtcNow.ToString("O"),
                Direction = "Inbound",
            },
        };

        var result = service.DetermineCompletion(pollingItem, gmrs);

        result.ShouldComplete.Should().BeFalse();
        result.Reason.Should().BeNull();
    }

    [Fact]
    public void DetermineCompletion_WithExpiredPollingItem_ShouldMarkComplete()
    {
        var service = new PollingItemCompletionService(_mockLogger.Object, _timeProvider);
        var pollingItem = new PollingItem
        {
            Id = "mrn123",
            Created = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-35),
            ExpiryDate = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-5),
        };

        var gmrs = new List<Gmr>
        {
            new()
            {
                GmrId = "gmr123",
                HaulierEori = "GB123",
                State = "Submitted",
                UpdatedDateTime = DateTime.UtcNow.ToString("O"),
                Direction = "Inbound",
            },
        };

        var result = service.DetermineCompletion(pollingItem, gmrs);

        result.ShouldComplete.Should().BeTrue();
        result.Reason.Should().Contain("expired");
        result.Reason.Should().Contain(pollingItem.ExpiryDate.ToString("yyyy-MM-dd"));
    }

    [Fact]
    public void DetermineCompletion_WithExpiredPollingItemAndNoGmrs_ShouldMarkComplete()
    {
        var service = new PollingItemCompletionService(_mockLogger.Object, _timeProvider);
        var pollingItem = new PollingItem
        {
            Id = "mrn123",
            Created = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-35),
            ExpiryDate = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-5),
        };

        var gmrs = new List<Gmr>();

        var result = service.DetermineCompletion(pollingItem, gmrs);

        result.ShouldComplete.Should().BeTrue();
        result.Reason.Should().Contain("expired");
    }

    [Fact]
    public void DetermineCompletion_WithNoGmrsAndNotExpired_ShouldNotMarkComplete()
    {
        var service = new PollingItemCompletionService(_mockLogger.Object, _timeProvider);
        var pollingItem = new PollingItem
        {
            Id = "mrn123",
            Created = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-5),
            ExpiryDate = _timeProvider.GetUtcNow().UtcDateTime.AddDays(25),
        };

        var gmrs = new List<Gmr>();

        var result = service.DetermineCompletion(pollingItem, gmrs);

        result.ShouldComplete.Should().BeFalse();
        result.Reason.Should().BeNull();
    }

    [Fact]
    public void DetermineCompletion_CompletedGmrTakesPriorityOverExpiry_ShouldReturnCompletedReason()
    {
        var service = new PollingItemCompletionService(_mockLogger.Object, _timeProvider);
        var pollingItem = new PollingItem
        {
            Id = "mrn123",
            Created = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-35),
            ExpiryDate = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-5), // Expired
        };

        var gmrs = new List<Gmr>
        {
            new()
            {
                GmrId = "gmr123",
                HaulierEori = "GB123",
                State = "COMPLETED",
                UpdatedDateTime = DateTime.UtcNow.ToString("O"),
                Direction = "Inbound",
            },
        };

        var result = service.DetermineCompletion(pollingItem, gmrs);

        result.ShouldComplete.Should().BeTrue();
        result.Reason.Should().Contain("All GMRs");
        result.Reason.Should().Contain("COMPLETED");
        result.Reason.Should().NotContain("expired");
    }

    [Fact]
    public void DetermineCompletion_WithMultipleGmrsAllCompleted_ShouldMarkComplete()
    {
        var service = new PollingItemCompletionService(_mockLogger.Object, _timeProvider);
        var pollingItem = new PollingItem
        {
            Id = "mrn123",
            Created = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-5),
            ExpiryDate = _timeProvider.GetUtcNow().UtcDateTime.AddDays(25),
        };

        var gmrs = new List<Gmr>
        {
            new()
            {
                GmrId = "gmr123",
                HaulierEori = "GB123",
                State = "COMPLETED",
                UpdatedDateTime = DateTime.UtcNow.ToString("O"),
                Direction = "Inbound",
            },
            new()
            {
                GmrId = "gmr456",
                HaulierEori = "GB456",
                State = "COMPLETED",
                UpdatedDateTime = DateTime.UtcNow.ToString("O"),
                Direction = "Inbound",
            },
        };

        var result = service.DetermineCompletion(pollingItem, gmrs);

        result.ShouldComplete.Should().BeTrue();
        result.Reason.Should().Contain("All GMRs");
        result.Reason.Should().Contain("COMPLETED");
    }

    [Fact]
    public void DetermineCompletion_WithCompletedStateCaseInsensitive_ShouldMarkComplete()
    {
        var service = new PollingItemCompletionService(_mockLogger.Object, _timeProvider);
        var pollingItem = new PollingItem
        {
            Id = "mrn123",
            Created = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-5),
            ExpiryDate = _timeProvider.GetUtcNow().UtcDateTime.AddDays(25),
        };

        var gmrs = new List<Gmr>
        {
            new()
            {
                GmrId = "gmr123",
                HaulierEori = "GB123",
                State = "completed", // lowercase
                UpdatedDateTime = DateTime.UtcNow.ToString("O"),
                Direction = "Inbound",
            },
        };

        var result = service.DetermineCompletion(pollingItem, gmrs);

        result.ShouldComplete.Should().BeTrue();
        result.Reason.Should().Contain("All GMRs");
        result.Reason.Should().Contain("COMPLETED");
    }

    [Fact]
    public void DetermineCompletion_LogsCompletionReason()
    {
        var service = new PollingItemCompletionService(_mockLogger.Object, _timeProvider);
        var pollingItem = new PollingItem
        {
            Id = "mrn123",
            Created = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-5),
            ExpiryDate = _timeProvider.GetUtcNow().UtcDateTime.AddDays(25),
        };

        var gmrs = new List<Gmr>
        {
            new()
            {
                GmrId = "gmr123",
                HaulierEori = "GB123",
                State = "COMPLETED",
                UpdatedDateTime = DateTime.UtcNow.ToString("O"),
                Direction = "Inbound",
            },
        };

        service.DetermineCompletion(pollingItem, gmrs);

        _mockLogger.Verify(
            x =>
                x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>(
                        (v, t) => v.ToString()!.Contains("mrn123") && v.ToString()!.Contains("complete")
                    ),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
    }
}
