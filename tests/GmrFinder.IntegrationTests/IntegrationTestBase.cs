using Amazon.SQS;
using GmrFinder.Configuration;
using GmrFinder.Data;
using GmrFinder.Extensions;
using GmrFinder.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Environment = System.Environment;

namespace GmrFinder.IntegrationTests;

[Trait("Category", "IntegrationTests")]
[Collection("Integration Tests")]
public abstract class IntegrationTestBase
{
    public const string PublishMessageQueueName = "trade_imports_matched_gmrs_processor";

    private readonly Dictionary<string, string> _environmentVariables = new()
    {
        { "AWS_ACCESS_KEY_ID", "test" },
        { "AWS_SECRET_ACCESS_KEY", "test" },
        { "AWS_REGION", "eu-west-2" },
        { "SNS_ENDPOINT", "http://localhost:4566" },
        { "SQS_ENDPOINT", "http://localhost:4566" },
        { "USE_LOCALSTACK", "true" },
    };

    protected readonly IConfiguration Configuration;
    protected readonly IMongoContext Mongo;
    protected readonly ServiceProvider ServiceProvider;

    protected IntegrationTestBase()
    {
        SetEnvironmentVariables();

        var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/GmrFinder"));
        Configuration = new ConfigurationBuilder()
            .SetBasePath(projectRoot)
            .AddJsonFile("appsettings.json", false)
            .AddJsonFile("appsettings.Development.json", false)
            .AddEnvironmentVariables()
            .Build();

        var sc = new ServiceCollection();
        sc.AddSingleton(Configuration);
        sc.AddOptions<LocalStackOptions>().Bind(Configuration);
        sc.AddValidateOptions<DataEventsQueueConsumerOptions>(DataEventsQueueConsumerOptions.SectionName);
        sc.AddValidateOptions<MongoConfig>(MongoConfig.SectionName);
        sc.AddSqsClient();

        sc.AddLogging(c => c.AddConsole());
        sc.Configure<MongoConfig>(Configuration.GetSection("Mongo"));
        sc.AddSingleton<IMongoDbClientFactory, MongoDbClientFactory>();
        sc.AddSingleton<IMongoContext, MongoContext>();
        sc.AddSingleton<MongoDbInitializer>();
        sc.AddSingleton<IStorageService, StubStorageService>();

        ServiceProvider = sc.BuildServiceProvider();

        Mongo = ServiceProvider.GetRequiredService<IMongoContext>();
        var initializer = ServiceProvider.GetRequiredService<MongoDbInitializer>();
        initializer.Init().Wait();
    }

    protected IMongoContext MongoContext => ServiceProvider.GetRequiredService<IMongoContext>();

    private void SetEnvironmentVariables()
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
