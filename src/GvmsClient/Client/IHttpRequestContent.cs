using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text.Json;

namespace Defra.TradeImportsGmrFinder.GvmsClient.Client;

internal interface IHttpRequestContent;

public static class HttpRequestContentExtensions
{
    public static string AsJsonString(this object @this) => JsonSerializer.Serialize(@this);

    internal static JsonContent AsJsonContent(this object @this) =>
        JsonContent.Create(@this, new MediaTypeHeaderValue(MediaTypeNames.Application.Json));
}
