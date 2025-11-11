using System;

namespace GmrFinder.Polling;

public interface IPollingService
{
    Task PollItems(CancellationToken cancellationToken);
    Task Process(PollingRequest request, CancellationToken cancellationToken);
}
