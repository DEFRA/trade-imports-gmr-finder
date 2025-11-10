using GvmsClient.Client;

namespace GvmsClient.Tests.Client;

public static class AccessTokenHelper
{
    private const string AllChar = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    public static Token Create()
    {
        var random = new Random();
        var resultToken = new string(
            Enumerable.Repeat(AllChar, 8).Select(token => token[random.Next(token.Length)]).ToArray()
        );

        return new Token
        {
            AccessToken = resultToken,
            ExpiresIn = (int)TimeSpan.FromHours(1).TotalSeconds,
            TokenType = "Bearer",
            RefreshToken = "sdfsdfsdfsdf",
        };
    }
}
