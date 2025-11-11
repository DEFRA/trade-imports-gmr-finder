using GmrFinder.Jobs;
using MongoDB.Driver;

namespace GmrFinder.Data;

public interface IMongoContext
{
    IMongoCollection<ScheduleToken> ScheduleTokens { get; }
    IMongoCollectionSet<PollingItem> PollingItems { get; }
}
