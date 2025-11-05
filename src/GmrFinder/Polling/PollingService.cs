using System.Text.Json;
using GmrFinder.Data;

namespace GmrFinder.Polling;

public class PollingService(ILogger<PollingService> logger, IMongoContext mongo) : IPollingService
{
    public async Task Process(PollingRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Polling service: {Request}", JsonSerializer.Serialize(request));

        var pollingItem = new PollingItem { Mrn = request.Mrn, ChedReferences = request.ChedReferences };

        await mongo.PollingItem.InsertOneAsync(pollingItem, null, cancellationToken);
    }
}
