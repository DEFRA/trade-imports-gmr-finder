using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Defra.TradeImportsGmrFinder.Domain.Events;
using Defra.TradeImportsGmrFinder.GvmsClient.Client;
using Defra.TradeImportsGmrFinder.GvmsClient.Contract;
using Defra.TradeImportsGmrFinder.GvmsClient.Contract.Requests;
using GmrFinder.Configuration;
using GmrFinder.Data;
using GmrFinder.Metrics;
using GmrFinder.Producers;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace GmrFinder.Polling;

[SuppressMessage(
    "Design",
    "S107:Methods should not have too many parameters",
    Justification = "All parameters are necessary dependencies"
)]
public class PollingService(
    ILogger<PollingService> logger,
    IMongoContext mongo,
    IGvmsApiClient gvmsApiClient,
    IMatchedGmrsProducer matchedGmrsProducer,
    IPollingItemCompletionService pollingItemCompletionService,
    IOptions<PollingServiceOptions> options,
    PollingMetrics pollingMetrics,
    TimeProvider? timeProvider = null
) : IPollingService
{
    private readonly PollingServiceOptions _options = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task Process(PollingRequest request, CancellationToken cancellationToken)
    {
        var filter = Builders<PollingItem>.Filter.Where(f => f.Id == request.Mrn);
        var update = Builders<PollingItem>.Update.Combine(
            Builders<PollingItem>.Update.SetOnInsert(u => u.Created, _timeProvider.GetUtcNow().UtcDateTime),
            Builders<PollingItem>.Update.SetOnInsert(
                u => u.ExpiryDate,
                _timeProvider.GetUtcNow().UtcDateTime.Add(_options.ExpiryTimeSpan)
            ),
            Builders<PollingItem>.Update.SetOnInsert(u => u.Complete, false),
            Builders<PollingItem>.Update.SetOnInsert(u => u.Gmrs, new Dictionary<string, string>()),
            Builders<PollingItem>.Update.SetOnInsert(u => u.LastPolled, null)
        );

        var existingPollingItem = await mongo.PollingItems.FindOneAndUpdate(
            filter,
            update,
            new FindOneAndUpdateOptions<PollingItem> { IsUpsert = true },
            cancellationToken
        );

        if (existingPollingItem is not null)
        {
            logger.LogInformation("Polling item for MRN {Mrn} already exists, skipping", request.Mrn);
            return;
        }

        logger.LogInformation("Inserted new polling item for {Mrn}", request.Mrn);
    }

    public async Task PollItems(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var pollItems = await mongo.PollingItems.FindMany(
            p => !p.Complete,
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

        logger.LogInformation("Polling GVMS for {MrnCount} MRNs: {Mrns}", mrns.Count, string.Join(",", mrns.Keys));

        var gvmsTimer = Stopwatch.StartNew();
        var results = (
            await gvmsApiClient.SearchForGmrs(
                new MrnSearchRequest { DeclarationIds = [.. mrns.Keys] },
                cancellationToken
            )
        ).Result;
        gvmsTimer.Stop();

        logger.LogInformation("GVMS poll completed in {ElapsedMs} ms", gvmsTimer.ElapsedMilliseconds);

        var gmrs = results.Gmrs.ToDictionary(p => p.GmrId, p => p);
        var gmrsByDeclarationId = results.GmrByDeclarationId.ToDictionary(
            p => p.dec,
            p => p.gmrs.Where(gmrId => gmrs.ContainsKey(gmrId)).Select(gmrId => gmrs[gmrId]).ToList()
        );

        var matchedMrnCount = gmrsByDeclarationId.Count;
        var unmatchedMrnCount = Math.Max(mrns.Count - matchedMrnCount, 0);
        logger.LogInformation(
            "GVMS response: Found {MatchedMrnCount} MRNs with GMRs, {UnmatchedMrnCount} without, {GmrCount} unique GMRs",
            matchedMrnCount,
            unmatchedMrnCount,
            gmrs.Count
        );

        var updateSummary = await UpdatePollingItemRecords(pollItems, gmrsByDeclarationId, now, cancellationToken);
        logger.LogInformation(
            "Updated {UpdatedCount} polling items, {ItemsWithGmrs} had GMRs, {CompletedCount} marked complete, {UpdatesMade} updates made",
            pollItems.Count,
            updateSummary.itemsWithGmrs,
            updateSummary.completedCount,
            updateSummary.updatesMade
        );

        var matchedCount = await PublishMatchedGmrRecords(pollItems, gmrsByDeclarationId, cancellationToken);
        if (matchedCount == 0)
        {
            logger.LogInformation("No changed GMRs to publish for polled MRNs");
            return;
        }

        logger.LogInformation("Published {MatchedCount} changed GMRs", matchedCount);
    }

    private async Task<(int completedCount, int itemsWithGmrs, int updatesMade)> UpdatePollingItemRecords(
        List<PollingItem> pollItems,
        Dictionary<string, List<Gmr>> gmrsByDeclarationId,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        var updateResults = pollItems
            .Select(p =>
            {
                var filter = Builders<PollingItem>.Filter.Eq(f => f.Id, p.Id);
                var update = Builders<PollingItem>.Update.Set(u => u.LastPolled, now);
                var hasGmrs = gmrsByDeclarationId.TryGetValue(p.Id, out var gmrs);
                gmrs ??= [];

                if (hasGmrs)
                    update = update.Set(u => u.Gmrs, gmrs.ToDictionary(g => g.GmrId, g => JsonSerializer.Serialize(g)));

                // Check if the polling item should be marked complete
                var result = pollingItemCompletionService.DetermineCompletion(p, gmrs);
                if (result.ShouldComplete)
                {
                    update = update.Set(u => u.Complete, true);
                    pollingMetrics.RecordItemLeave(PollingMetrics.MrnQueueName, result);
                }

                return new
                {
                    Update = new UpdateOneModel<PollingItem>(filter, update),
                    HasGmrs = hasGmrs,
                    result.ShouldComplete,
                };
            })
            .ToList();

        var updates = updateResults.Select(WriteModel<PollingItem> (r) => r.Update).ToList();
        if (updates.Count != 0)
            await mongo.PollingItems.BulkWrite(updates, cancellationToken);

        var completedCount = updateResults.Count(r => r.ShouldComplete);
        var itemsWithGmrs = updateResults.Count(r => r.HasGmrs);
        return (completedCount, itemsWithGmrs, updates.Count);
    }

    private async Task<int> PublishMatchedGmrRecords(
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
        return changedRecords.Count;
    }
}
