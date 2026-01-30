using System.Diagnostics.CodeAnalysis;
using FluentValidation;
using GmrFinder.Configuration;
using GmrFinder.Consumers;
using GmrFinder.Data;
using GmrFinder.Endpoints;
using GmrFinder.Extensions;
using GmrFinder.Jobs;
using GmrFinder.Metrics;
using GmrFinder.Polling;
using GmrFinder.Processing;
using GmrFinder.Producers;
using GmrFinder.Services;
using GmrFinder.Utils;
using GmrFinder.Utils.Http;
using GmrFinder.Utils.Logging;
using GmrFinder.Utils.Validators;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Driver.Authentication.AWS;
using Serilog;

var app = CreateWebApplication(args);

// Ensure the database indices are initialized before starting the application.
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<MongoDbInitializer>();
    await initializer.Init();
}

await app.RunAsync();
return;

[ExcludeFromCodeCoverage]
static WebApplication CreateWebApplication(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);
    ConfigureBuilder(builder);

    var app = builder.Build();

    return SetupApplication(app);
}

[ExcludeFromCodeCoverage]
static void ConfigureBuilder(WebApplicationBuilder builder)
{
    builder.Configuration.AddEnvironmentVariables();

    // Load certificates into Trust Store - Note must happen before Mongo and Http client connections.
    builder.Services.AddCustomTrustStore();

    // Configure logging to use the CDP Platform standards.
    builder.Services.AddHttpContextAccessor();
    builder.Host.UseSerilog(CdpLogging.Configuration);

    // Default HTTP Client
    builder.Services.AddHttpClient("DefaultClient").AddHeaderPropagation();

    // Proxy HTTP Client
    builder.Services.AddTransient<ProxyHttpMessageHandler>();
    builder.Services.AddHttpClient("proxy").ConfigurePrimaryHttpMessageHandler<ProxyHttpMessageHandler>();

    // Propagate trace header.
    builder.Services.AddHeaderPropagation(options =>
    {
        var traceHeader = builder.Configuration.GetValue<string>("TraceHeader");
        if (!string.IsNullOrWhiteSpace(traceHeader))
            options.Headers.Add(traceHeader);
    });

    builder.Services.Configure<Dictionary<string, ScheduledJob>>(builder.Configuration.GetSection("ScheduledJobs"));

    MongoClientSettings.Extensions.AddAWSAuthentication();
    builder.Services.Configure<MongoConfig>(builder.Configuration.GetSection(MongoConfig.SectionName));
    builder.Services.AddSingleton<IMongoDbClientFactory, MongoDbClientFactory>();
    builder.Services.AddSingleton<IMongoContext, MongoContext>();
    builder.Services.AddSingleton<MongoDbInitializer>();

    builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));

    builder.Services.AddSingleton<IStringValidators, StringValidators>();

    builder.Services.AddGvmsApiClientService();
    builder.Services.AddOptions<LocalStackOptions>().Bind(builder.Configuration);
    builder.Services.AddOptions<FeatureOptions>().Bind(builder.Configuration);

    builder.Services.AddValidateOptions<DataEventsQueueConsumerOptions>(DataEventsQueueConsumerOptions.SectionName);
    builder.Services.AddValidateOptions<MatchedGmrsProducerOptions>(MatchedGmrsProducerOptions.SectionName);
    builder.Services.AddSqsClient();
    builder.Services.AddSnsClient();
    builder.Services.AddS3Client();

    var featureOptions = builder.Configuration.Get<FeatureOptions>() ?? new FeatureOptions();
    if (featureOptions.EnableSnsProducer)
        builder.Services.AddSingleton<IMatchedGmrsProducer, MatchedGmrsProducer>();
    else
        builder.Services.AddSingleton<IMatchedGmrsProducer, StubMatchedGmrsProducer>();

    if (featureOptions.EnableStorage)
        builder.Services.AddSingleton<IStorageService, StorageService>();
    else
        builder.Services.AddSingleton<IStorageService, StubStorageService>();

    builder.Services.AddSingleton<ICustomsDeclarationProcessor, CustomsDeclarationProcessor>();
    builder.Services.AddSingleton<IImportPreNotificationProcessor, ImportPreNotificationProcessor>();

    builder.Services.AddValidateOptions<PollingServiceOptions>(PollingServiceOptions.SectionName);
    builder.Services.AddSingleton<IPollingItemCompletionService, PollingItemCompletionService>();
    builder.Services.AddSingleton<IPollingService, PollingService>();

    if (featureOptions.EnableSqsConsumer)
        builder.Services.AddHostedService<DataEventsQueueConsumer>();

    builder.Services.AddTransient<IScheduleTokenProvider, MongoDbScheduleTokenProvider>();
    builder.Services.AddHostedService<PollGvmsByMrn>();

    builder.Services.AddHealthChecks();

    builder.Services.AddSingleton<PollingMetrics>();
    builder.Services.AddSingleton<ScheduledJobMetrics>();
    builder.Services.AddSingleton<ConsumerMetrics>();

    builder.Services.AddValidatorsFromAssemblyContaining<Program>();
}

[ExcludeFromCodeCoverage]
static WebApplication SetupApplication(WebApplication app)
{
    app.UseHeaderPropagation();
    app.UseRouting();
    app.MapHealthChecks("/health");
    var featureOptions = app.Services.GetRequiredService<IOptions<FeatureOptions>>().Value;

    if (!featureOptions.EnableSqsConsumer)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogWarning("SQS Queue consumption is disabled via ENABLE_SQS_CONSUMER feature flag");
    }

    if (!featureOptions.EnableSnsProducer)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogWarning("SNS message production is disabled via ENABLE_SNS_PRODUCER feature flag");
    }

    if (featureOptions.EnableDevEndpoints)
        app.MapConsumerEndpoints();
    app.UseEmfExporter(app.Environment.ApplicationName);

    return app;
}
