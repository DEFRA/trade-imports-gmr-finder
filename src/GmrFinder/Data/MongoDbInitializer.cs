using System.Diagnostics.CodeAnalysis;
using GmrFinder.Jobs;
using MongoDB.Driver;

namespace GmrFinder.Data;

public class MongoDbInitializerException(Exception inner) : Exception("Failed to initialize mongodb", inner);

[ExcludeFromCodeCoverage]
public class MongoDbInitializer(IMongoDbClientFactory database, ILogger<MongoDbInitializer> logger)
{
    public async Task Init(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Updating Mongo indexes");
        await InitPollingServiceCollection(cancellationToken);
        await InitScheduleTokenCollection(cancellationToken);
    }

    private async Task InitScheduleTokenCollection(CancellationToken cancellationToken)
    {
        await CreateIndex(
            new CreateIndexModel<ScheduleToken>(
                Builders<ScheduleToken>.IndexKeys.Ascending(x => x.scheduleKey).Ascending(x => x.scheduleExecutionTime),
                new CreateIndexOptions
                {
                    Name = "UniqueSchedule",
                    Background = false,
                    Unique = true,
                }
            ),
            cancellationToken
        );
    }

    private async Task InitPollingServiceCollection(CancellationToken cancellationToken)
    {
        await CreateIndex(
            new CreateIndexModel<PollingItem>(
                Builders<PollingItem>.IndexKeys.Ascending(x => x.Complete).Ascending(x => x.LastPolled),
                new CreateIndexOptions { Name = "PollingItemsBatchQuery" }
            ),
            cancellationToken
        );
    }

    private async Task CreateIndex<T>(CreateIndexModel<T> indexModel, CancellationToken cancellationToken = default)
    {
        var indexName = indexModel.Options.Name;

        try
        {
            logger.LogInformation("Creating index on {IndexName} - {Collection}", indexName, typeof(T).Name);

            await database
                .GetCollection<T>(typeof(T).Name)
                .Indexes.CreateOneAsync(indexModel, cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to create index {IndexName} on {Collection}", indexName, typeof(T).Name);
            throw new MongoDbInitializerException(e);
        }
    }
}
