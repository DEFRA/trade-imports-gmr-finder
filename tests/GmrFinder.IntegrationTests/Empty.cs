using FluentAssertions;

namespace GmrFinder.IntegrationTests;

public class Empty
{
    // This exists to provide a test that does not have a category of IntegrationTests to avoid an error
    [Fact]
    public void EmptyTest()
    {
        true.Should().BeTrue();
    }
}
