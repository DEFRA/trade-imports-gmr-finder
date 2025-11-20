using System.Text.Json.Serialization;

namespace Defra.TradeImportsGmrFinder.GvmsClient.Contract.Responses;

public class TrnSearchResponse
{
    [JsonPropertyName("gmrs")]
    public List<GmrLite> Gmrs { get; set; } = [];
}
