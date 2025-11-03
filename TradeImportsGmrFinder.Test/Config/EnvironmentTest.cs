using Microsoft.AspNetCore.Builder;

namespace TradeImportsGmrFinder.Test.Config;

public class EnvironmentTest
{

   [Fact]
   public void IsNotDevModeByDefault()
   { 
       var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
       var isDev = TradeImportsGmrFinder.Config.Environment.IsDevMode(builder);
       Assert.False(isDev);
   }
}
