using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace GmrFinder.Data;

[ExcludeFromCodeCoverage]
public class MongoCollectionSet<T>(IMongoDbClientFactory database) : IMongoCollectionSet<T>
    where T : class, IDataEntity
{
    private IQueryable<T> Queryable => Collection.AsQueryable();
    public IMongoCollection<T> Collection => database.GetCollection<T>(typeof(T).Name);

    public async Task BulkWrite(List<WriteModel<T>> operations, CancellationToken cancellationToken)
    {
        await Collection.BulkWriteAsync(operations, new BulkWriteOptions { IsOrdered = false }, cancellationToken);
    }

    public async Task<T?> FindOne(Expression<Func<T, bool>> expression, CancellationToken cancellationToken)
    {
        return await Queryable.SingleOrDefaultAsync(expression, cancellationToken);
    }

    public async Task<List<T>> FindMany<TKey>(
        Expression<Func<T, bool>> where,
        CancellationToken cancellationToken,
        Expression<Func<T, TKey>>? orderBy = null,
        int? limit = null
    )
    {
        var query = Queryable.Where(where);

        if (orderBy is not null)
            query = query.OrderBy(orderBy);
        if (limit.HasValue)
            query = query.Take(limit.Value);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task Insert(T item, CancellationToken cancellationToken)
    {
        await Collection.InsertOneAsync(item, null, cancellationToken);
    }

    public async Task<T?> FindOneAndUpdate(
        FilterDefinition<T> filter,
        UpdateDefinition<T> update,
        FindOneAndUpdateOptions<T> options,
        CancellationToken cancellationToken
    )
    {
        return await Collection.FindOneAndUpdateAsync(filter, update, options, cancellationToken);
    }

    public async Task<long> DeleteMany(FilterDefinition<T> filter, CancellationToken cancellationToken)
    {
        var result = await Collection.DeleteManyAsync(filter, cancellationToken);
        return result.DeletedCount;
    }
}
