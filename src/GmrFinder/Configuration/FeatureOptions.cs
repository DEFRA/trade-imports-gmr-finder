namespace GmrFinder.Configuration;

public class FeatureOptions
{
    [ConfigurationKeyName("ENABLE_DEV_ENDPOINTS")]
    public bool EnableDevEndpoints { get; init; } = false;

    [ConfigurationKeyName("DEV_ENDPOINT_USERNAME")]
    public string? DevEndpointUsername { get; init; }

    [ConfigurationKeyName("DEV_ENDPOINT_PASSWORD")]
    public string? DevEndpointPassword { get; init; }

    [ConfigurationKeyName("ENABLE_SQS_CONSUMER")]
    public bool EnableSqsConsumer { get; init; } = false;

    [ConfigurationKeyName("ENABLE_SNS_PRODUCER")]
    public bool EnableSnsProducer { get; init; } = false;

    [ConfigurationKeyName("ENABLE_STORAGE")]
    public bool EnableStorage { get; init; } = false;

    [ConfigurationKeyName("ENABLE_GMR_POLLING")]
    public bool EnableGmrPolling { get; init; } = false;
}
