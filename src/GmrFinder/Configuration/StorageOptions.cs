namespace GmrFinder.Configuration;

public class StorageOptions
{
    [ConfigurationKeyName("SEARCH_RESULTS_BUCKET")]
    public string? SearchResultStorageBucket { get; init; }
}
