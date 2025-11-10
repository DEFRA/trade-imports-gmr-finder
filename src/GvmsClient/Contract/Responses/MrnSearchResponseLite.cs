using System.Text.Json.Serialization;

namespace GvmsClient.Contract.Responses;

public class MrnSearchResponseLite
{
    [JsonPropertyName("gmrByDeclarationId")]
    public List<GmrDeclaration> GmrByDeclarationId { get; set; } = [];

    [JsonPropertyName("gmrs")]
    public List<GmrLite> Gmrs { get; set; } = [];
}
