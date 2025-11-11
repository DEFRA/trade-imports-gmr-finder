using System.Text.Json.Serialization;

namespace GvmsClient.Contract;

public class GmrReportToLocations
{
    [JsonPropertyName("inspectionTypeId")]
    public required string InspectionTypeId { get; init; }

    [JsonPropertyName("locationIds")]
    public required List<string> LocationIds { get; init; }
}
