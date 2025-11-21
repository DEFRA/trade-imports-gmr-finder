using System.Diagnostics.CodeAnalysis;

namespace Defra.TradeImportsGmrFinder.GvmsClient.Contract.Responses;

[ExcludeFromCodeCoverage]
public class ActualCrossing
{
    public string RouteId { get; set; } = string.Empty;
    public DateTime LocalDateTimeOfArrival { get; set; }
}
