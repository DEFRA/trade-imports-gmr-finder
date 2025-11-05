using System;

namespace GmrFinder.Polling;

public interface IPollingService
{
    Task Process(PollingRequest request, CancellationToken cancellationToken);
}
