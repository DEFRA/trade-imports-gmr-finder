using Amazon;
using Amazon.Runtime;
using Amazon.SQS;

namespace GmrFinder.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSqsClient(this IServiceCollection services, IConfiguration configuration)
    {
        var clientId = configuration.GetValue<string>("AWS_ACCESS_KEY_ID");
        var clientSecret = configuration.GetValue<string>("AWS_SECRET_ACCESS_KEY");

        if (!string.IsNullOrEmpty(clientSecret) && !string.IsNullOrEmpty(clientId))
        {
            var region = configuration.GetValue<string>("AWS_REGION") ?? RegionEndpoint.EUWest2.ToString();
            var regionEndpoint = RegionEndpoint.GetBySystemName(region);

            services.AddSingleton<IAmazonSQS>(sp => new AmazonSQSClient(
                new BasicAWSCredentials(clientId, clientSecret),
                new AmazonSQSConfig
                {
                    // https://github.com/aws/aws-sdk-net/issues/1781
                    AuthenticationRegion = region,
                    RegionEndpoint = regionEndpoint,
                    ServiceURL = configuration.GetValue<string>("SQS_ENDPOINT"),
                }
            ));

            return services;
        }

        services.AddSingleton<IAmazonSQS>(sp => new AmazonSQSClient());
        return services;
    }
}
