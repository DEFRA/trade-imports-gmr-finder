namespace GmrFinder.Polling;

public class PollingRequest
{
    private string _mrn = string.Empty;

    public required string Mrn
    {
        get => _mrn;
        init => _mrn = value.ToUpperInvariant();
    }
}
