using Amazon.SQS;
using GmrFinder.Configuration;
using GmrFinder.Data;
using GmrFinder.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Environment = System.Environment;

namespace GmrFinder.IntegrationTests;

[Trait("Category", "IntegrationTests")]
[Collection("Integration Tests")]
public abstract class IntegrationTestBase
{
    private readonly Dictionary<string, string> _environmentVariables = new()
    {
        { "AWS_ACCESS_KEY_ID", "test" },
        { "AWS_SECRET_ACCESS_KEY", "test" },
        { "AWS_REGION", "eu-west-2" },
        { "SQS_ENDPOINT", "http://localhost:4566" },
    };

    public readonly IConfiguration Configuration;
    public readonly ServiceProvider ServiceProvider;

    protected IntegrationTestBase()
    {
        SetEnvironmentVariables();

        var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/GmrFinder"));
        Configuration = new ConfigurationBuilder()
            .SetBasePath(projectRoot)
            .AddJsonFile("appsettings.json", false)
            .AddJsonFile("appsettings.Development.json", true)
            .AddEnvironmentVariables()
            .Build();

        var sc = new ServiceCollection();
        sc.AddSingleton(Configuration);
        sc.AddValidateOptions<DataEventsQueueConsumerOptions>(DataEventsQueueConsumerOptions.SectionName);
        sc.AddValidateOptions<MongoConfig>(MongoConfig.SectionName);
        sc.AddSqsClient(Configuration);

        sc.AddLogging(c => c.AddConsole());
        sc.AddSingleton<IMongoDbClientFactory, MongoDbClientFactory>();
        sc.AddSingleton<IMongoContext, MongoContext>();
        sc.AddSingleton<MongoDbInitializer>();

        ServiceProvider = sc.BuildServiceProvider();

        var initializer = ServiceProvider.GetRequiredService<MongoDbInitializer>();
        initializer.Init().Wait();
    }

    protected IMongoContext MongoContext => ServiceProvider.GetRequiredService<IMongoContext>();

    protected void SetEnvironmentVariables()
    {
        foreach (var (key, value) in _environmentVariables)
        {
            if (Environment.GetEnvironmentVariable(key) != null)
                continue;

            Environment.SetEnvironmentVariable(key, value);
        }
    }

    protected async Task<(IAmazonSQS, string)> GetSqsClient(string queueName)
    {
        var sqsClient = ServiceProvider.GetRequiredService<IAmazonSQS>();
        return (sqsClient, (await sqsClient.GetQueueUrlAsync(queueName)).QueueUrl);
    }
}
