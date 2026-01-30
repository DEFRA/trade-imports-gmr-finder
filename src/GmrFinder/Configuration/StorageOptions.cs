namespace GmrFinder.Configuration;

public class StorageOptions
{
    public const string SectionName = "Storage";
    public string? SearchResultsBucket { get; init; }
}
