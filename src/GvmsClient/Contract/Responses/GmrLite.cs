using System.Text.Json.Serialization;

namespace GvmsClient.Contract.Responses;

public class GmrLite
{
    [JsonPropertyName("gmrId")]
    public string GmrId { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = string.Empty;

    [JsonPropertyName("actualCrossing")]
    public ActualCrossing ActualCrossing { get; set; } = new();

    [JsonPropertyName("trailerRegistrationNums")]
    public List<string> TrailerRegistrationNums { get; set; } = [];
}
