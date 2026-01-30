using System.Diagnostics.CodeAnalysis;

namespace GmrFinder.Services;

[ExcludeFromCodeCoverage]
public class StubStorageService(ILogger<StubStorageService> logger, TimeProvider? timeProvider = null) : IStorageService
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public Task TryStoreSearchResultsAsync(string content)
    {
        var key = StorageService.CreateKey(_timeProvider.GetUtcNow().Date);
        logger.LogInformation("Would stored search results to: '{Key}'", key);
        return Task.CompletedTask;
    }
}
