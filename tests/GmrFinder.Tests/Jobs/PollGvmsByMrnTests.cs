using GmrFinder.Configuration;
using GmrFinder.Jobs;
using GmrFinder.Metrics;
using GmrFinder.Polling;
using GmrFinder.Tests.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace GmrFinder.Tests.Jobs;

public sealed class PollGvmsByMrnTests
{
    [Fact]
    public async Task DoWork_Should_Log_And_Invoke_PollingService()
    {
        // Arrange
        var logger = new Mock<ILogger<PollGvmsByMrn>>();
        var scheduleTokenProvider = new Mock<IScheduleTokenProvider>();
        var pollingService = new Mock<IPollingService>();

        var metrics = new ScheduledJobMetrics(new MockMeterFactory().CreateMeter());

        var options = Options.Create(
            new Dictionary<string, ScheduledJob> { ["poll_gvms_by_mrn"] = new() { Cron = "* * * * * *" } }
        );

        string? capturedTraceId = null;
        CancellationToken capturedToken = default;

        pollingService
            .Setup(x => x.PollItems(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>(
                (traceId, token) =>
                {
                    capturedTraceId = traceId;
                    capturedToken = token;
                }
            )
            .Returns(Task.CompletedTask);

        var sut = new TestablePollGvmsByMrn(
            logger.Object,
            scheduleTokenProvider.Object,
            options,
            metrics,
            pollingService.Object
        );

        var cancellationToken = CancellationToken.None;

        // Act
        await sut.InvokeDoWork(cancellationToken);

        // Assert
        pollingService.Verify(x => x.PollItems(It.IsAny<string>(), cancellationToken), Times.Once);

        capturedTraceId.Should().NotBeNull();
        capturedTraceId.Should().HaveLength(32);

        logger.VerifyLog(LogLevel.Information, $"Executing {nameof(PollGvmsByMrn)}", Times.Once());
    }

    [Fact]
    public async Task DoWork_Should_Create_LoggingScope_With_CorrelationId()
    {
        // Arrange
        var logger = new Mock<ILogger<PollGvmsByMrn>>();
        var scheduleTokenProvider = new Mock<IScheduleTokenProvider>();
        var pollingService = new Mock<IPollingService>();

        var metrics = new ScheduledJobMetrics(new MockMeterFactory().CreateMeter());

        var options = Options.Create(
            new Dictionary<string, ScheduledJob> { ["poll_gvms_by_mrn"] = new() { Cron = "* * * * * *" } }
        );

        object? capturedScope = null;

        logger
            .Setup(x => x.BeginScope(It.IsAny<It.IsAnyType>()))
            .Callback<object>(scope => capturedScope = scope)
            .Returns(Mock.Of<IDisposable>());

        var sut = new TestablePollGvmsByMrn(
            logger.Object,
            scheduleTokenProvider.Object,
            options,
            metrics,
            pollingService.Object
        );

        // Act
        await sut.InvokeDoWork(CancellationToken.None);

        // Assert
        capturedScope.Should().NotBeNull();

        var scopeDictionary = capturedScope.Should().BeAssignableTo<Dictionary<string, object>>().Subject;

        scopeDictionary.Should().ContainKey("x-correlation-id");

        var correlationId = scopeDictionary["x-correlation-id"]?.ToString();

        correlationId.Should().NotBeNullOrWhiteSpace();
        correlationId.Should().HaveLength(32);
    }

    private sealed class TestablePollGvmsByMrn(
        ILogger<PollGvmsByMrn> logger,
        IScheduleTokenProvider scheduleTokenProvider,
        IOptions<Dictionary<string, ScheduledJob>> config,
        ScheduledJobMetrics scheduledJobMetrics,
        IPollingService pollingService
    ) : PollGvmsByMrn(logger, scheduleTokenProvider, config, scheduledJobMetrics, pollingService)
    {
        public Task InvokeDoWork(CancellationToken cancellationToken) => base.DoWork(cancellationToken);
    }
}