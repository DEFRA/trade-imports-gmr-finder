using Microsoft.AspNetCore.Builder;

namespace GmrFinder.Tests.Config;

public class EnvironmentTest
{
    [Fact]
    public void IsNotDevModeByDefault()
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        var isDev = Configuration.Environment.IsDevMode(builder);
        Assert.False(isDev);
    }
}
