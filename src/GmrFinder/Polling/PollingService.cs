using System.Text.Json;
using Domain.Events;
using GmrFinder.Configuration;
using GmrFinder.Data;
using GmrFinder.Producers;
using GvmsClient.Client;
using GvmsClient.Contract;
using GvmsClient.Contract.Requests;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace GmrFinder.Polling;

public class PollingService(
    ILogger<PollingService> logger,
    IMongoContext mongo,
    IGvmsApiClient gvmsApiClient,
    IMatchedGmrsProducer matchedGmrsProducer,
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

        var gmrs = results.Gmrs.ToDictionary(p => p.GmrId, p => p);
        var gmrsByDeclarationId = results.GmrByDeclarationId.ToDictionary(
            p => p.dec,
            p => p.gmrs.Where(gmrId => gmrs.ContainsKey(gmrId)).Select(gmrId => gmrs[gmrId]).ToList()
        );

        await UpdatePollingItemRecords(pollItems, gmrsByDeclarationId, now, cancellationToken);
        await PublishMatchedGmrRecords(pollItems, gmrsByDeclarationId, cancellationToken);
    }

    private async Task UpdatePollingItemRecords(
        List<PollingItem> pollItems,
        Dictionary<string, List<Gmr>> gmrsByDeclarationId,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        var recordsToUpdate = pollItems
            .Select(p =>
            {
                var filter = Builders<PollingItem>.Filter.Eq(f => f.Id, p.Id);
                var update = Builders<PollingItem>.Update.Set(u => u.LastPolled, now);

                if (!gmrsByDeclarationId.TryGetValue(p.Id, out var gmrs))
                {
                    return new UpdateOneModel<PollingItem>(filter, update);
                }

                update = update.Set(u => u.Gmrs, gmrs.ToDictionary(g => g.GmrId, g => JsonSerializer.Serialize(g)));

                return new UpdateOneModel<PollingItem>(filter, update);
            })
            .ToList();

        await mongo.PollingItems.BulkWrite([.. recordsToUpdate], cancellationToken);
    }

    private async Task PublishMatchedGmrRecords(
        List<PollingItem> pollItems,
        Dictionary<string, List<Gmr>> gmrsByDeclarationId,
        CancellationToken cancellationToken
    )
    {
        var changedRecords = pollItems
            .Where(p => gmrsByDeclarationId.ContainsKey(p.Id))
            .Select(p =>
            {
                var existingGmrs = p.Gmrs.ToDictionary(g => g.Key, g => JsonSerializer.Deserialize<Gmr>(g.Value));
                var updatedGmrs = gmrsByDeclarationId[p.Id];

                var changedGmrs = updatedGmrs.Where(g =>
                    !existingGmrs.ContainsKey(g.GmrId) || existingGmrs[g.GmrId]!.UpdatedDateTime != g.UpdatedDateTime
                );

                return changedGmrs.Select(g => new MatchedGmr { Mrn = p.Id, Gmr = g }).ToList();
            })
            .SelectMany(x => x)
            .ToList();

        await matchedGmrsProducer.PublishMatchedGmrs(changedRecords, cancellationToken);
    }
}
