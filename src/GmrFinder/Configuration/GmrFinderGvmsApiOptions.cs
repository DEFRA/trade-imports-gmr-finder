using Defra.TradeImportsGmrFinder.GvmsClient.Client;
using Microsoft.Extensions.Http.Resilience;

namespace GmrFinder.Configuration;

public class GmrFinderGvmsApiOptions : GvmsApiOptions
{
    public const string SectionName = "GvmsApi";

    public HttpCircuitBreakerStrategyOptions CircuitBreaker { get; init; } = new();
    public HttpRetryStrategyOptions Retry { get; init; } = new() { MaxRetryAttempts = 3 };
    public HttpTimeoutStrategyOptions Timeout { get; init; } = new() { Timeout = TimeSpan.FromSeconds(30) };
}
