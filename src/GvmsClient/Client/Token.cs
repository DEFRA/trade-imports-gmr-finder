using System.Text.Json.Serialization;

namespace GvmsClient.Client;

public class Token
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    public TimeSpan GetExpires() => TimeSpan.FromSeconds(ExpiresIn);

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;
}
