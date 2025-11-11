using System.Text.Json.Serialization;

namespace GvmsClient.Contract;

public class GvmsResponse
{
    [JsonPropertyName("gmrByDeclarationId")]
    public List<GmrDeclaration> GmrByDeclarationId { get; set; } = [];

    [JsonPropertyName("gmrs")]
    public List<Gmr> Gmrs { get; set; } = [];
}
