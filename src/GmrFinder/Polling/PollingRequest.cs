using System;

namespace GmrFinder.Polling;

public class PollingRequest
{
    public required HashSet<string> ChedReferences { get; init; }
    public required string Mrn { get; init; }
}
