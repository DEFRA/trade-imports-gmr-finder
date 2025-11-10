using GmrFinder.Jobs;
using MongoDB.Driver;

namespace GmrFinder.Data;

public class MongoDbScheduleTokenProvider(IMongoContext mongoContext) : IScheduleTokenProvider
{
    public async Task<bool> TryGetExecutionTokenAsync(
        string scheduleName,
        DateTime scheduleExecutionTime,
        DateTime currentTime
    )
    {
        try
        {
            await mongoContext.ScheduleTokens.InsertOneAsync(
                new ScheduleToken(scheduleName, scheduleExecutionTime, currentTime)
            );
            return true;
        }
        catch (MongoWriteException e) when (e.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }
}
