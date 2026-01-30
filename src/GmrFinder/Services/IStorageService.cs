namespace GmrFinder.Services;

public interface IStorageService
{
    Task TryStoreSearchResultsAsync(string content);
}
