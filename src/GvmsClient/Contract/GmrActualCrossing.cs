using System.Text.Json.Serialization;

namespace Defra.TradeImportsGmrFinder.GvmsClient.Contract;

public class GmrActualCrossing : GmrCrossing
{
    [JsonPropertyName("localDateTimeOfArrival")]
    public required string LocalDateTimeOfArrival { get; set; } = string.Empty;
}
