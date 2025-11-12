using System.Text.Json;
using GmrFinder.Configuration;
using GmrFinder.Data;
using GvmsClient.Client;
using GvmsClient.Contract.Requests;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace GmrFinder.Polling;

public class PollingService(
    ILogger<PollingService> logger,
    IMongoContext mongo,
    IGvmsApiClient gvmsApiClient,
    IOptions<PollingServiceOptions> options,
    TimeProvider? timeProvider = null
) : IPollingService
{
    private readonly PollingServiceOptions _options = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task Process(PollingRequest request, CancellationToken cancellationToken)
    {
        var existingPollingItem = await mongo.PollingItems.FindOne(p => p.Id == request.Mrn, cancellationToken);

        if (existingPollingItem is not null)
        {
            logger.LogInformation("Polling item for MRN {Mrn} already exists, skipping", request.Mrn);
            return;
        }

        logger.LogInformation("Inserting new polling item for {Mrn}", request.Mrn);
        var pollingItem = new PollingItem { Id = request.Mrn, Created = _timeProvider.GetUtcNow().UtcDateTime };

        await mongo.PollingItems.Insert(pollingItem, cancellationToken);
    }

    public async Task PollItems(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var pollItems = await mongo.PollingItems.FindMany(
            where: p => !p.Complete,
            orderBy: p => p.LastPolled ?? DateTime.MinValue,
            limit: _options.MaxPollSize,
            cancellationToken: cancellationToken
        );

        var mrns = pollItems.ToDictionary(p => p.Id, p => p);

        if (mrns.Keys.Count == 0)
        {
            logger.LogInformation("No MRNs to poll for");
            return;
        }

        logger.LogInformation("Polling MRNs: {Mrns}", string.Join(",", mrns.Keys));

        var results = (
            await gvmsApiClient.SearchForGmrs(
                new MrnSearchRequest { DeclarationIds = [.. mrns.Keys] },
                cancellationToken
            )
        ).Result;

        if (results.GmrByDeclarationId.Count == 0)
        {
            logger.LogInformation("No poll results found for MRNs");
        }
        else
        {
            logger.LogInformation("Received GMRs for MRNs");
        }

        var gmrsByDeclarationId = results.GmrByDeclarationId.ToDictionary(p => p.dec, p => p.gmrs);
        var gmrs = results.Gmrs.ToDictionary(p => p.GmrId, p => p);

        var recordsToUpdate = pollItems
            .Select(p =>
            {
                var filter = Builders<PollingItem>.Filter.Eq(f => f.Id, p.Id);
                var update = Builders<PollingItem>.Update.Set(u => u.LastPolled, now);

                if (!gmrsByDeclarationId.TryGetValue(p.Id, out var gmrIds))
                {
                    return new UpdateOneModel<PollingItem>(filter, update);
                }

                update = update.Set(
                    u => u.Gmrs,
                    gmrIds
                        .Where(gmrId => gmrs.ContainsKey(gmrId))
                        .ToDictionary(gmrId => gmrId, gmrId => JsonSerializer.Serialize(gmrs[gmrId]))
                );

                return new UpdateOneModel<PollingItem>(filter, update);
            })
            .ToList();

        await mongo.PollingItems.BulkWrite([.. recordsToUpdate], cancellationToken);
    }
}
