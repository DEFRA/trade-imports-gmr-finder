using System.Text.Json.Serialization;
using GvmsClient.Client;

namespace GvmsClient.Contract.Requests;

public class MrnSearchRequest(params string[] mrns) : IHttpRequestContent
{
    [JsonPropertyName("declarationIds")]
    public string[] DeclarationIds { get; set; } = mrns;
}
