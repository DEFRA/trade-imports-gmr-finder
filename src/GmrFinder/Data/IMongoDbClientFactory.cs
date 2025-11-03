using MongoDB.Driver;

namespace GmrFinder.Data;

public interface IMongoDbClientFactory
{
    IMongoClient GetClient();

    IMongoCollection<T> GetCollection<T>(string collection);
}
