using System.Text.Json.Serialization;

namespace GvmsClient.Contract.Responses;

public class TrnSearchResponse
{
    [JsonPropertyName("gmrs")]
    public List<GmrLite> Gmrs { get; set; } = [];
}
