using System.Text.Json.Serialization;

namespace Defra.TradeImportsGmrFinder.GvmsClient.Contract;

public class GvmsResponse
{
    [JsonPropertyName("gmrByDeclarationId")]
    public List<GmrDeclaration> GmrByDeclarationId { get; set; } = [];

    [JsonPropertyName("gmrs")]
    public List<Gmr> Gmrs { get; set; } = [];
}
