using System.Diagnostics.CodeAnalysis;
using FluentValidation;
using GmrFinder.Configuration;
using GmrFinder.Consumers;
using GmrFinder.Data;
using GmrFinder.Extensions;
using GmrFinder.Polling;
using GmrFinder.Processing;
using GmrFinder.Utils;
using GmrFinder.Utils.Http;
using GmrFinder.Utils.Logging;
using Serilog;

var app = CreateWebApplication(args);
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
        {
            options.Headers.Add(traceHeader);
        }
    });

    builder.Services.Configure<MongoConfig>(builder.Configuration.GetSection("Mongo"));
    builder.Services.AddSingleton<IMongoDbClientFactory, MongoDbClientFactory>();

    builder.Services.AddValidateOptions<DataEventsQueueConsumerOptions>(DataEventsQueueConsumerOptions.SectionName);
    builder.Services.AddSqsClient(builder.Configuration);
    builder.Services.AddSingleton<IPollingService, PollingService>();
    builder.Services.AddSingleton<ICustomsDeclarationProcessor, CustomsDeclarationProcessor>();
    builder.Services.AddSingleton<IImportPreNotificationProcessor, ImportPreNotificationProcessor>();
    builder.Services.AddHostedService<DataEventsQueueConsumer>();

    builder.Services.AddHealthChecks();

    builder.Services.AddValidatorsFromAssemblyContaining<Program>();
}

[ExcludeFromCodeCoverage]
static WebApplication SetupApplication(WebApplication app)
{
    app.UseHeaderPropagation();
    app.UseRouting();
    app.MapHealthChecks("/health");

    return app;
}
