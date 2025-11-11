using System.Text.Json.Serialization;

namespace GvmsClient.Contract;

public class GmrActualCrossing : GmrCrossing
{
    [JsonPropertyName("localDateTimeOfArrival")]
    public required string LocalDateTimeOfArrival { get; set; } = string.Empty;
}
