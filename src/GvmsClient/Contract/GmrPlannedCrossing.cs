using System.Text.Json.Serialization;

namespace Defra.TradeImportsGmrFinder.GvmsClient.Contract;

public class GmrPlannedCrossing : GmrCrossing
{
    [JsonPropertyName("localDateTimeOfDeparture")]
    public string? LocalDateTimeOfDeparture { get; set; }
}
