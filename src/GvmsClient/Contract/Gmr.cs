using System.Text.Json.Serialization;

namespace Defra.TradeImportsGmrFinder.GvmsClient.Contract;

public class Gmr
{
    [JsonPropertyName("gmrId")]
    public required string GmrId { get; init; }

    [JsonPropertyName("haulierEORI")]
    public required string HaulierEori { get; init; }

    [JsonPropertyName("state")]
    public required string State { get; set; } = string.Empty;

    [JsonPropertyName("inspectionRequired")]
    public bool? InspectionRequired { get; init; }

    [JsonPropertyName("reportToLocations")]
    public List<GmrReportToLocations>? ReportToLocations { get; init; }

    [JsonPropertyName("updatedDateTime")]
    public required string UpdatedDateTime { get; init; }

    [JsonPropertyName("direction")]
    public required string Direction { get; set; } = string.Empty;

    [JsonPropertyName("haulierType")]
    public string? HaulierType { get; init; }

    [JsonPropertyName("isUnaccompanied")]
    public bool? IsUnaccompanied { get; init; }

    [JsonPropertyName("vehicleRegNum")]
    public string? VehicleRegNum { get; init; }

    [JsonPropertyName("trailerRegistrationNums")]
    public List<string>? TrailerRegistrationNums { get; init; }

    [JsonPropertyName("containerReferenceNums")]
    public List<string>? ContainerReferenceNums { get; init; }

    [JsonPropertyName("plannedCrossing")]
    public GmrPlannedCrossing? PlannedCrossing { get; init; }

    [JsonPropertyName("checkedInCrossing")]
    public GmrCheckedInCrossing? CheckedInCrossing { get; init; }

    [JsonPropertyName("actualCrossing")]
    public GmrActualCrossing? ActualCrossing { get; init; }

    [JsonPropertyName("declarations")]
    public GmrDeclarationResponse? Declarations { get; init; }
}
