using System.Text.Json.Serialization;

namespace Defra.TradeImportsGmrFinder.GvmsClient.Contract;

public class GmrDeclarationEntityResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
}
