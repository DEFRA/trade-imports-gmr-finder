namespace GmrFinder.Data;

public class PollingItem
{
    public string? Mrn { get; init; } = null;
    public HashSet<string> ChedReferences { get; init; } = [];
}
