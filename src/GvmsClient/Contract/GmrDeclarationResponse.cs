using System.Text.Json.Serialization;

namespace GvmsClient.Contract;

public class GmrDeclarationResponse
{
    [JsonPropertyName("transits")]
    public List<GmrDeclarationEntityResponse>? Transits { get; init; }

    [JsonPropertyName("customs")]
    public List<GmrDeclarationEntityResponse>? Customs { get; init; }
}
