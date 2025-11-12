namespace GmrFinder.Data;

public class PollingItem : IDataEntity
{
    public string Id { get; set; } = string.Empty;

    public Dictionary<string, string> Gmrs { get; init; } = [];
    public bool Complete { get; init; } = false;
    public DateTime Created { get; init; }
    public DateTime? LastPolled { get; init; }
}
