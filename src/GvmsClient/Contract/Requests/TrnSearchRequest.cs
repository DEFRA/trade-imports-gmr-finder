using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Defra.TradeImportsGmrFinder.GvmsClient.Client;

namespace Defra.TradeImportsGmrFinder.GvmsClient.Contract.Requests;

[ExcludeFromCodeCoverage]
public class TrnSearchRequest(params string[] trns) : IHttpRequestContent
{
    [JsonPropertyName("trailerRegistrationNums")]
    public string[] TrailerRegistrationNums { get; set; } = trns;
}
