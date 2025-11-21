using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Defra.TradeImportsGmrFinder.GvmsClient.Contract.Responses;

[ExcludeFromCodeCoverage]
public class TrnSearchResponse
{
    [JsonPropertyName("gmrs")]
    public List<GmrLite> Gmrs { get; set; } = [];
}
