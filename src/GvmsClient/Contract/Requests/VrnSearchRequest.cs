using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Defra.TradeImportsGmrFinder.GvmsClient.Client;

namespace Defra.TradeImportsGmrFinder.GvmsClient.Contract.Requests;

[ExcludeFromCodeCoverage]
public class VrnSearchRequest(params string[] vrns) : IHttpRequestContent
{
    [JsonPropertyName("vrns")]
    public string[] Vrns { get; } = vrns;
}
