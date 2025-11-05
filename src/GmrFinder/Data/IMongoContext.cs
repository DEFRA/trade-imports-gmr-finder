using System;
using MongoDB.Driver;

namespace GmrFinder.Data;

public interface IMongoContext
{
    IMongoCollection<PollingItem> PollingItem { get; }
}
