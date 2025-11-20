using System.Text.Json.Serialization;

namespace Defra.TradeImportsGmrFinder.GvmsClient.Contract;

public class GmrCheckedInCrossing : GmrCrossing
{
    [JsonPropertyName("localDateTimeOfArrival")]
    public required string LocalDateTimeOfArrival { get; set; } = string.Empty;
}
