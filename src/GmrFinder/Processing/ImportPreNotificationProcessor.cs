using Defra.TradeImportsDataApi.Domain.Events;
using Defra.TradeImportsDataApi.Domain.Ipaffs;
using GmrFinder.Metrics;
using GmrFinder.Polling;
using GmrFinder.Utils.Validators;

namespace GmrFinder.Processing;

public class ImportPreNotificationProcessor(
    ILogger<ImportPreNotificationProcessor> logger,
    IPollingService pollingService,
    IStringValidators stringValidators,
    PollingMetrics pollingMetrics
) : IImportPreNotificationProcessor
{
    public async Task ProcessAsync(
        ResourceEvent<ImportPreNotification> importPreNotification,
        CancellationToken cancellationToken
    )
    {
        var chedReference = importPreNotification.ResourceId;
        var nctsMrn = importPreNotification
            .Resource?.ExternalReferences?.Where(r => r.System == "NCTS")
            .Select(r => r.Reference!)
            .FirstOrDefault();

        logger.LogInformation(
            "Received import pre notification, CHED reference: '{ChedReference}', MRN: '{NctsMrn}'",
            chedReference,
            nctsMrn
        );

        if (nctsMrn == null)
        {
            logger.LogInformation(
                "Skipping Ipaffs record {ChedReference} because it does not have an NCTS MRN",
                chedReference
            );
            return;
        }

        if (!stringValidators.IsValidMrn(nctsMrn))
        {
            logger.LogInformation(
                "Skipping Ipaffs record {ChedReference} due to invalid NCTS MRN: {Mrn}",
                chedReference,
                nctsMrn
            );
            return;
        }

        logger.LogInformation(
            "Sending new/updated CHED {ChedReference} with MRN {NctsMrn} to the polling service",
            chedReference,
            nctsMrn
        );
        pollingMetrics.RecordItemJoined(PollingMetrics.MrnQueueName, PollingMetrics.ItemSource.ImportNotification);

        await pollingService.Process(new PollingRequest { Mrn = nctsMrn }, cancellationToken);
    }
}
