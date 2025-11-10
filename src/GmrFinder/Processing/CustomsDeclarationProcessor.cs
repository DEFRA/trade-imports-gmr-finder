using Defra.TradeImportsDataApi.Domain.CustomsDeclaration;
using Defra.TradeImportsDataApi.Domain.Events;
using GmrFinder.Polling;

namespace GmrFinder.Processing;

public class CustomsDeclarationProcessor(ILogger<CustomsDeclarationProcessor> logger, IPollingService pollingService)
    : ICustomsDeclarationProcessor
{
    public async Task ProcessAsync(
        ResourceEvent<CustomsDeclaration> customsDeclaration,
        CancellationToken cancellationToken
    )
    {
        var mrn = customsDeclaration.ResourceId;
        var chedReferences =
            customsDeclaration
                .Resource?.ClearanceDecision?.Results?.Select(result => result.ImportPreNotification)
                .Where(reference => reference is not null)
                .Select(reference => reference!)
                .ToHashSet() ?? [];
        var portOfArrival = customsDeclaration.Resource?.ClearanceRequest?.GoodsLocationCode;

        logger.LogInformation(
            "Received customs declaration, MRN: '{Mrn}' - CHEDs: '{ChedReferences}' - Port of Arrival: '{PortOfArrival}'",
            mrn,
            string.Join(",", chedReferences),
            portOfArrival
        );

        if (chedReferences.Count == 0)
        {
            logger.LogInformation("Skipping MRN {Mrn} because there are no CHEDs associated", mrn);
            return;
        }

        if (portOfArrival is null || !portOfArrival.EndsWith("GVM"))
        {
            logger.LogInformation(
                "Skipping MRN {Mrn} because the port {PortOfArrival} is a non-GVMS port",
                mrn,
                portOfArrival
            );
            return;
        }

        logger.LogInformation("Processing MRN {Mrn}", mrn);

        await pollingService.Process(new PollingRequest { Mrn = mrn }, cancellationToken);
    }
}
