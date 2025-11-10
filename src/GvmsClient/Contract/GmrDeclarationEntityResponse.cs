using System.Text.Json.Serialization;

namespace GvmsClient.Contract;

public class GmrDeclarationEntityResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
}
