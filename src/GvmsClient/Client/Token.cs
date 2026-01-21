using System.Text.Json.Serialization;

namespace Defra.TradeImportsGmrFinder.GvmsClient.Client;

public class Token
{
    private const int ExpiryLatencyAdjustment = 60;

    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;

    public TimeSpan GetExpires()
    {
        return TimeSpan.FromSeconds(Math.Min(Math.Abs(ExpiresIn - ExpiryLatencyAdjustment), ExpiresIn));
    }
}
