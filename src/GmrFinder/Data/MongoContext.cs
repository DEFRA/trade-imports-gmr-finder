using System;
using MongoDB.Driver;

namespace GmrFinder.Data;

public class MongoContext(IMongoDbClientFactory database) : IMongoContext
{
    internal IMongoCollection<PollingItem> _pollingItem = database.GetCollection<PollingItem>(nameof(PollingItem));

    public IMongoCollection<PollingItem> PollingItem
    {
        get => _pollingItem;
    }
}
