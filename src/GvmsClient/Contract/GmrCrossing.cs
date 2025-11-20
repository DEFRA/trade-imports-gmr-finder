using System.Text.Json.Serialization;

namespace Defra.TradeImportsGmrFinder.GvmsClient.Contract;

public class GmrCrossing
{
    [JsonPropertyName("routeId")]
    public string RouteId { get; set; } = string.Empty;
}
