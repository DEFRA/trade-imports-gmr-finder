using System;
using Elastic.CommonSchema;
using MongoDB.Driver;

namespace GmrFinder.Data;

public class MongoIndexService(IMongoDbClientFactory database, ILogger<MongoIndexService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Updating Mongo indexes");
        await CreatePollingItemIndexes(cancellationToken);
    }

    public async Task CreatePollingItemIndexes(CancellationToken cancellationToken)
    {
        await CreateIndex(
            "PollingItemMrn",
            Builders<PollingItem>.IndexKeys.Ascending(x => x.Mrn),
            cancellationToken: cancellationToken
        );
    }

    private async Task CreateIndex<T>(
        string name,
        IndexKeysDefinition<T> keys,
        bool unique = false,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var indexModel = new CreateIndexModel<T>(
                keys,
                new CreateIndexOptions
                {
                    Name = name,
                    Background = true,
                    Unique = unique,
                }
            );

            logger.LogInformation("Creating index on {Name} - {Collection}", name, typeof(T).Name);

            await database
                .GetCollection<T>(typeof(T).Name)
                .Indexes.CreateOneAsync(indexModel, cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to create index {Name} on {Collection}", name, typeof(T).Name);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
