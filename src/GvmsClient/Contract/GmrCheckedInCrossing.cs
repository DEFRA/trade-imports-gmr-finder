using System.Text.Json.Serialization;

namespace GvmsClient.Contract;

public class GmrCheckedInCrossing : GmrCrossing
{
    [JsonPropertyName("localDateTimeOfArrival")]
    public required string LocalDateTimeOfArrival { get; set; } = string.Empty;
}
