using System.Text.Json.Serialization;

namespace GvmsClient.Contract;

public class GmrPlannedCrossing : GmrCrossing
{
    [JsonPropertyName("localDateTimeOfDeparture")]
    public string? LocalDateTimeOfDeparture { get; set; }
}
