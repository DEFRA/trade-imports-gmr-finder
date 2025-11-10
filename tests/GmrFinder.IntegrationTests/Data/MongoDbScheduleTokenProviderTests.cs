using GmrFinder.Data;

namespace GmrFinder.IntegrationTests.Data;

public class MongoDbScheduleTokenProviderTests : IntegrationTestBase
{
    [Fact]
    public async Task TryGetExecutionTokenAsync_WhenInsertSucceeds_ShouldReturnTrue()
    {
        var scheduledTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var scheduleName = $"test_schedule_{Guid.NewGuid()}";

        var provider = new MongoDbScheduleTokenProvider(MongoContext);

        var result1 = await provider.TryGetExecutionTokenAsync(scheduleName, scheduledTime, DateTime.UtcNow);
        var result2 = await provider.TryGetExecutionTokenAsync(
            scheduleName,
            scheduledTime.AddSeconds(2),
            DateTime.UtcNow
        );

        Assert.True(result1, "Should have been able to get token");
        Assert.True(result2, "Should have been able to get token");
    }

    [Fact]
    public async Task TryGetExecutionTokenAsync_WhenInsertDuplicateScheduleOccurs_ShouldReturnFalse()
    {
        var scheduledTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var scheduleName = $"test_schedule_{Guid.NewGuid()}";

        var provider = new MongoDbScheduleTokenProvider(MongoContext);

        var result1 = await provider.TryGetExecutionTokenAsync(scheduleName, scheduledTime, DateTime.UtcNow);
        var result2 = await provider.TryGetExecutionTokenAsync(scheduleName, scheduledTime, DateTime.UtcNow);

        Assert.True(result1, "Should have been able to get token");
        Assert.False(result2, "Should not have been able to get token");
    }
}
