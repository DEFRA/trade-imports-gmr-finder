using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Defra.TradeImportsGmrFinder.GvmsClient.Contract.Responses;

[ExcludeFromCodeCoverage]
public class MrnSearchResponseLite
{
    [JsonPropertyName("gmrByDeclarationId")]
    public List<GmrDeclaration> GmrByDeclarationId { get; set; } = [];

    [JsonPropertyName("gmrs")]
    public List<GmrLite> Gmrs { get; set; } = [];
}
