using Cronos;
using GmrFinder.Jobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace GmrFinder.Tests.Jobs;

public class CronHostedServiceTests
{
    private const string Every1StSecondCronExpression = "*/1 * * * * *";
    private const string Every2NdSecondCronExpression = "*/2 * * * * *";
    private const string Every5ThSecondCronExpression = "*/5 * * * * *";

    private readonly ILogger<CronHostedService> _logger = Substitute.For<ILogger<CronHostedService>>();

    [Fact]
    public async Task ExecuteAsync_ShouldExecuteWorkOnSchedule()
    {
        var fakeTimeProvider = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var service = new TestCronHostedService(_logger, Every2NdSecondCronExpression, fakeTimeProvider);

        using var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);

        fakeTimeProvider.Advance(TimeSpan.FromSeconds(2));
        await Task.Delay(100, cts.Token);

        fakeTimeProvider.Advance(TimeSpan.FromSeconds(2));
        await Task.Delay(100, cts.Token);

        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        Assert.True(service.ExecutionCount > 1, "Service should have executed more than once");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSkipExecutionWhereCannotObtainToken()
    {
        var startDateTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var fakeTimeProvider = new FakeTimeProvider(startDateTime);
        var scheduleTokenProvider = Substitute.For<IScheduleTokenProvider>();
        var expectedFirstExecutionTime = startDateTime.AddSeconds(2);

        scheduleTokenProvider
            .TryGetExecutionTokenAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(callInfo =>
            {
                var scheduleExecutionTime = callInfo.ArgAt<DateTime>(1);
                return scheduleExecutionTime != expectedFirstExecutionTime;
            });

        var service = new TestCronHostedService(
            _logger,
            Every2NdSecondCronExpression,
            fakeTimeProvider,
            scheduleTokenProvider
        );

        using var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);

        fakeTimeProvider.Advance(TimeSpan.FromSeconds(2));
        await Task.Delay(100, cts.Token);

        fakeTimeProvider.Advance(TimeSpan.FromSeconds(2));
        await Task.Delay(100, cts.Token);

        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        Assert.True(
            service.ExecutionCount == 1,
            $"Service should have executed only once but was {service.ExecutionCount}"
        );
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleExceptionInDoWork()
    {
        var fakeTimeProvider = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var service = new TestCronHostedService(
            _logger,
            Every1StSecondCronExpression,
            fakeTimeProvider,
            null,
            _ => throw new InvalidOperationException("Test exception")
        );

        using var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);
        fakeTimeProvider.Advance(TimeSpan.FromSeconds(1));
        await Task.Delay(100, cts.Token);

        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        Assert.True(service.ExecutionCount == 1, "Service should have attempted execution");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldContinueAfterException()
    {
        var fakeTimeProvider = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var service = new TestCronHostedService(
            _logger,
            Every1StSecondCronExpression,
            fakeTimeProvider,
            null,
            executionCount =>
            {
                if (executionCount == 1)
                    throw new InvalidOperationException("Test exception");
            }
        );

        using var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);

        fakeTimeProvider.Advance(TimeSpan.FromSeconds(1));
        await Task.Delay(100, cts.Token);

        fakeTimeProvider.Advance(TimeSpan.FromSeconds(1));
        await Task.Delay(100, cts.Token);

        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        Assert.True(service.ExecutionCount >= 2, "Service should continue executing after exception");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldContinueAfterTaskCanceledExceptionFromWork()
    {
        var fakeTimeProvider = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var service = new TestCronHostedService(
            _logger,
            Every1StSecondCronExpression,
            fakeTimeProvider,
            null,
            executionCount =>
            {
                if (executionCount == 1)
                    throw new TaskCanceledException("Simulated timeout");
            }
        );

        using var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);

        fakeTimeProvider.Advance(TimeSpan.FromSeconds(1));
        await Task.Delay(100, cts.Token);

        fakeTimeProvider.Advance(TimeSpan.FromSeconds(1));
        await Task.Delay(100, cts.Token);

        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        Assert.True(service.ExecutionCount >= 2, "Service should continue executing after TaskCanceledException");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStopWhenCancellationRequested()
    {
        var fakeTimeProvider = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var service = new TestCronHostedService(_logger, Every1StSecondCronExpression, fakeTimeProvider);

        using var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);
        await Task.Delay(50, cts.Token);

        await service.StopAsync(cts.Token);

        Assert.Equal(0, service.ExecutionCount);
    }

    [Fact]
    public async Task Constructor_WithNullTimeProvider_ShouldUseSystemTimeProvider()
    {
        var service = new TestCronHostedService(_logger, Every5ThSecondCronExpression);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await service.StartAsync(cts.Token);
        await Task.Delay(50, cts.Token);

        Assert.Equal(0, service.ExecutionCount);
    }

    [Fact]
    public void Constructor_WithInvalidCronExpression_ShouldThrowException()
    {
        Assert.Throws<CronFormatException>(() => new TestCronHostedService(_logger, "invalid cron"));
    }

    private class TestScheduleTokenProvider : IScheduleTokenProvider
    {
        public Task<bool> TryGetExecutionTokenAsync(
            string scheduleName,
            DateTime scheduleExecutionTime,
            DateTime currentTime
        )
        {
            return Task.FromResult(true);
        }
    }

    private class TestCronHostedService(
        ILogger<CronHostedService> logger,
        string cronExpression,
        TimeProvider? timeProvider = null,
        IScheduleTokenProvider? scheduleTokenProvider = null,
        Action<int>? action = null
    )
        : CronHostedService(
            logger,
            scheduleTokenProvider ?? new TestScheduleTokenProvider(),
            cronExpression,
            "test_schedule",
            timeProvider
        )
    {
        public int ExecutionCount { get; private set; }

        protected override async Task DoWork(CancellationToken cancellationToken)
        {
            ExecutionCount++;
            action?.Invoke(ExecutionCount);
            await Task.CompletedTask;
        }
    }
}
