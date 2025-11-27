using GmrFinder.Polling;

namespace GmrFinder.Tests.Polling;

public class PollingRequestTests
{
    [Fact]
    public void PollingRequest_WhenInstantiated_UppercasesTheMrn()
    {
        var pollingRequest = new PollingRequest { Mrn = "mrn123" };
        pollingRequest.Mrn.Should().Be("MRN123");
    }
}
