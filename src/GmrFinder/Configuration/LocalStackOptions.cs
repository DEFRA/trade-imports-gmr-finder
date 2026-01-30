using System.Diagnostics.CodeAnalysis;

namespace GmrFinder.Configuration;

[ExcludeFromCodeCoverage]
public class LocalStackOptions
{
    [ConfigurationKeyName("AWS_ACCESS_KEY_ID")]
    public string? AccessKeyId { get; init; }

    [ConfigurationKeyName("AWS_REGION")]
    public string? AwsRegion { get; init; }

    [ConfigurationKeyName("AWS_SECRET_ACCESS_KEY")]
    public string? SecretAccessKey { get; init; }

    [ConfigurationKeyName("SNS_ENDPOINT")]
    public string? SnsEndpoint { get; init; }

    [ConfigurationKeyName("SQS_ENDPOINT")]
    public string? SqsEndpoint { get; init; }

    [ConfigurationKeyName("S3_ENDPOINT")]
    public string? S3Endpoint { get; init; }

    [ConfigurationKeyName("USE_LOCALSTACK")]
    public bool? UseLocalStack { get; init; } = false;
}
