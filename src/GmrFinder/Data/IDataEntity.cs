using MongoDB.Bson.Serialization.Attributes;

namespace GmrFinder.Data;

public interface IDataEntity
{
    [BsonId]
    string Id { get; set; }
}
