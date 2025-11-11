using System.Text.Json.Serialization;
using GvmsClient.Client;

namespace GvmsClient.Contract.Requests;

public class VrnSearchRequest(params string[] vrns) : IHttpRequestContent
{
    [JsonPropertyName("vrns")]
    public string[] Vrns { get; } = vrns;
}
