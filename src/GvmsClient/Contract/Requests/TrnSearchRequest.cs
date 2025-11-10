using System.Text.Json.Serialization;
using GvmsClient.Client;

namespace GvmsClient.Contract.Requests;

public class TrnSearchRequest(params string[] trns) : IHttpRequestContent
{
    [JsonPropertyName("trailerRegistrationNums")]
    public string[] TrailerRegistrationNums { get; set; } = trns;
}
