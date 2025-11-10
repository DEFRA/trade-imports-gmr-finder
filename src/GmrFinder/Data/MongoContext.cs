using GmrFinder.Jobs;
using MongoDB.Driver;

namespace GmrFinder.Data;

public class MongoContext(IMongoDbClientFactory database) : IMongoContext
{
    public IMongoCollection<ScheduleToken> ScheduleTokens { get; } = database.GetCollection<ScheduleToken>(
        nameof(ScheduleToken)
    );

    public IMongoCollectionSet<PollingItem> PollingItems { get; } = new MongoCollectionSet<PollingItem>(database);
}
