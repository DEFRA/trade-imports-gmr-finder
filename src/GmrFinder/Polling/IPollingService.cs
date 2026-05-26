using System;

namespace GmrFinder.Polling;

public interface IPollingService
{
    Task PollItems(string pollId, CancellationToken cancellationToken);
    Task Process(PollingRequest request, CancellationToken cancellationToken);
}
