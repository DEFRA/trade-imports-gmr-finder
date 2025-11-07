using GmrFinder.Jobs;
using MongoDB.Driver;

namespace GmrFinder.Data;

public class MongoContext(IMongoDbClientFactory database) : IMongoContext
{
    private readonly IMongoCollection<ScheduleToken> _scheduleTokens = database.GetCollection<ScheduleToken>(
        nameof(ScheduleToken)
    );

    public IMongoCollection<ScheduleToken> ScheduleTokens => _scheduleTokens;
}
