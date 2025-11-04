using Amazon.SQS;
using GmrFinder.Configuration;
using GmrFinder.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Environment = System.Environment;

namespace GmrFinder.IntegrationTests;

[Trait("Category", "IntegrationTests")]
[Collection("Integration Tests")]
public abstract class IntegrationTestBase
{
    public readonly IConfiguration Configuration;
    public readonly ServiceProvider ServiceProvider;

    private readonly Dictionary<string, string> _environmentVariables = new Dictionary<string, string>()
    {
        { "AWS_ACCESS_KEY_ID", "test" },
        { "AWS_SECRET_ACCESS_KEY", "test" },
        { "AWS_REGION", "eu-west-2" },
        { "SQS_ENDPOINT", "http://localhost:4566" },
    };

    protected IntegrationTestBase()
    {
        SetEnvironmentVariables();

        var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/GmrFinder"));
        Configuration = new ConfigurationBuilder()
            .SetBasePath(projectRoot)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var sc = new ServiceCollection();
        sc.AddSingleton(Configuration);
        sc.AddValidateOptions<DataEventsQueueConsumerOptions>(DataEventsQueueConsumerOptions.SectionName);
        sc.AddSqsClient(Configuration);

        ServiceProvider = sc.BuildServiceProvider();
    }

    protected void SetEnvironmentVariables()
    {
        foreach (var (key, value) in _environmentVariables)
        {
            if (Environment.GetEnvironmentVariable(key) != null)
            {
                continue;
            }

            Environment.SetEnvironmentVariable(key, value);
        }
    }

    protected async Task<(IAmazonSQS, string)> GetSqsClient(string queueName)
    {
        var sqsClient = ServiceProvider.GetRequiredService<IAmazonSQS>();
        return (sqsClient, (await sqsClient.GetQueueUrlAsync(queueName)).QueueUrl);
    }
}
