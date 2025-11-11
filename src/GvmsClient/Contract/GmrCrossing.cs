using System.Text.Json.Serialization;

namespace GvmsClient.Contract;

public class GmrCrossing
{
    [JsonPropertyName("routeId")]
    public string RouteId { get; set; } = string.Empty;
}
