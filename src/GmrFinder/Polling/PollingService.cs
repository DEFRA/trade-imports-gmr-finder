using System.Text.Json;

namespace GmrFinder.Polling;

public class PollingService(ILogger<PollingService> logger) : IPollingService
{
    public Task Process(PollingRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Polling service: {Request}", JsonSerializer.Serialize(request));
        return Task.CompletedTask;
    }
}
