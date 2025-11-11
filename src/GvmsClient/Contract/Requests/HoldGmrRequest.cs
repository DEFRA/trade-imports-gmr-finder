using System.Text.Json.Serialization;
using GvmsClient.Client;

namespace GvmsClient.Contract.Requests;

public class HoldGmrRequest(bool hold) : IHttpRequestContent
{
    [JsonPropertyName("hold")]
    public bool Hold { get; set; } = hold;
}
