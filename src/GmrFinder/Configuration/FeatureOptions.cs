namespace GmrFinder.Configuration;

public class FeatureOptions
{
    [ConfigurationKeyName("ENABLE_DEV_ENDPOINTS")]
    public bool EnableDevEndpoints { get; init; } = false;

    [ConfigurationKeyName("DEV_ENDPOINT_USERNAME")]
    public string? DevEndpointUsername { get; init; }

    [ConfigurationKeyName("DEV_ENDPOINT_PASSWORD")]
    public string? DevEndpointPassword { get; init; }
}
