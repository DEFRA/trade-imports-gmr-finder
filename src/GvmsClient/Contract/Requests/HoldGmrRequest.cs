using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Defra.TradeImportsGmrFinder.GvmsClient.Client;

namespace Defra.TradeImportsGmrFinder.GvmsClient.Contract.Requests;

[ExcludeFromCodeCoverage]
public class HoldGmrRequest(bool hold) : IHttpRequestContent
{
    [JsonPropertyName("hold")]
    public bool Hold { get; set; } = hold;
}
