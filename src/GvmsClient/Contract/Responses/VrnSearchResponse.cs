using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Defra.TradeImportsGmrFinder.GvmsClient.Contract.Responses;

[ExcludeFromCodeCoverage]
public class VrnSearchResponse
{
    [JsonPropertyName("gmrsByVRN")]
    public List<GmrByVrn> GmrsByVrn { get; set; } = [];

    [JsonPropertyName("gmrs")]
    public List<GmrLite> Gmrs { get; set; } = [];
}
