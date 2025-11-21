using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Defra.TradeImportsGmrFinder.GvmsClient.Client;

namespace Defra.TradeImportsGmrFinder.GvmsClient.Contract.Requests;

[ExcludeFromCodeCoverage]
public class MrnSearchRequest(params string[] mrns) : IHttpRequestContent
{
    [JsonPropertyName("declarationIds")]
    public string[] DeclarationIds { get; set; } = mrns;
}
