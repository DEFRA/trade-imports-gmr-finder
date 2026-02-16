using System.Diagnostics.Metrics;
using System.Linq.Expressions;
using System.Text.Json;
using Defra.TradeImportsGmrFinder.Domain.Events;
using Defra.TradeImportsGmrFinder.GvmsClient.Client;
using Defra.TradeImportsGmrFinder.GvmsClient.Contract;
using Defra.TradeImportsGmrFinder.GvmsClient.Contract.Requests;
using GmrFinder.Configuration;
using GmrFinder.Data;
using GmrFinder.Metrics;
using GmrFinder.Polling;
using GmrFinder.Producers;
using GmrFinder.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Moq;

namespace GmrFinder.Tests.Polling;

public class PollingServiceTests
{
    private readonly Mock<IMeterFactory> _meterFactory = new();
    private readonly Mock<IPollingItemCompletionService> _mockCompletionService = new();
    private readonly Mock<IGvmsApiClientService> _mockGvmsApiClient = new();
    private readonly Mock<IMatchedGmrsProducer> _mockMatchedGmrsProducer = new();
    private readonly Mock<IStorageService> _mockStorageService = new();

    private readonly TimeProvider _mockTimeProvider = new FakeTimeProvider(
        new DateTimeOffset(2025, 11, 7, 11, 10, 15, TimeSpan.Zero)
    );

    private readonly IOptions<PollingServiceOptions> _options = Options.Create(new PollingServiceOptions());

    public PollingServiceTests()
    {
        _meterFactory.Setup(x => x.Create(It.IsAny<MeterOptions>())).Returns(new Meter("test"));
    }

    [Fact]
    public async Task Process_PollingItem_ShouldBeUpserted()
    {
        const string expectedMrn = "mrn123";

        var mockPollingItemCollection = new Mock<IMongoCollectionSet<PollingItem>>();
        var contextMock = new Mock<IMongoContext>();
        contextMock.Setup(x => x.PollingItems).Returns(mockPollingItemCollection.Object);
        mockPollingItemCollection.Setup(x =>
            x.FindOneAndUpdate(
                It.IsAny<FilterDefinition<PollingItem>>(),
                It.IsAny<UpdateDefinition<PollingItem>>(),
                It.IsAny<FindOneAndUpdateOptions<PollingItem>>(),
                It.IsAny<CancellationToken>()
            )
        );

        var service = new PollingService(
            Mock.Of<ILogger<PollingService>>(),
            contextMock.Object,
            _mockGvmsApiClient.Object,
            _mockMatchedGmrsProducer.Object,
            _mockCompletionService.Object,
            _options,
            new PollingMetrics(_meterFactory.Object),
            _mockStorageService.Object,
            _mockTimeProvider
        );
        var request = new PollingRequest { Mrn = expectedMrn };

        var renderArgs = new RenderArgs<PollingItem>(
            BsonSerializer.SerializerRegistry.GetSerializer<PollingItem>(),
            BsonSerializer.SerializerRegistry
        );

        BsonValue? filter = null;
        BsonValue? update = null;
        FindOneAndUpdateOptions<PollingItem>? options = null;

        mockPollingItemCollection
            .Setup(x =>
                x.FindOneAndUpdate(
                    It.IsAny<FilterDefinition<PollingItem>>(),
                    It.IsAny<UpdateDefinition<PollingItem>>(),
                    It.IsAny<FindOneAndUpdateOptions<PollingItem>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (
                    FilterDefinition<PollingItem> f,
                    UpdateDefinition<PollingItem> u,
                    FindOneAndUpdateOptions<PollingItem> o,
                    CancellationToken _
                ) =>
                {
                    filter = f.Render(renderArgs);
                    update = u.Render(renderArgs);
                    options = o;
                }
            );

        await service.Process(request, CancellationToken.None);

        filter!["_id"].Should().Be("MRN123");

        var updateDoc = update!["$setOnInsert"].AsBsonDocument;
        updateDoc["Created"].ToUniversalTime().Should().Be(_mockTimeProvider.GetUtcNow().UtcDateTime);
        updateDoc["ExpiryDate"].ToUniversalTime().Should().Be(_mockTimeProvider.GetUtcNow().UtcDateTime.AddDays(30));
        updateDoc["Complete"].AsBoolean.Should().BeFalse();
        updateDoc["Gmrs"].AsBsonDocument.ElementCount.Should().Be(0);
        updateDoc["LastPolled"].Should().Be(BsonNull.Value);

        options!.IsUpsert.Should().BeTrue();
    }

    [Fact]
    public async Task Process_WithExistingPollingItem_LogsSkippingMessage()
    {
        var logger = new CollectingLogger<PollingService>();
        var mockPollingItemCollection = new Mock<IMongoCollectionSet<PollingItem>>();
        var contextMock = new Mock<IMongoContext>();
        contextMock.Setup(x => x.PollingItems).Returns(mockPollingItemCollection.Object);
        mockPollingItemCollection
            .Setup(x =>
                x.FindOneAndUpdate(
                    It.IsAny<FilterDefinition<PollingItem>>(),
                    It.IsAny<UpdateDefinition<PollingItem>>(),
                    It.IsAny<FindOneAndUpdateOptions<PollingItem>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new PollingItem { Id = "MRN123" });

        var service = new PollingService(
            logger,
            contextMock.Object,
            _mockGvmsApiClient.Object,
            _mockMatchedGmrsProducer.Object,
            _mockCompletionService.Object,
            _options,
            new PollingMetrics(_meterFactory.Object),
            _mockStorageService.Object,
            _mockTimeProvider
        );

        await service.Process(new PollingRequest { Mrn = "mrn123" }, CancellationToken.None);

        logger.Entries.Should().ContainSingle();
        var entry = logger.Entries.Single();
        entry.LogMessage.Should().Be("Polling item for MRN {Mrn} already exists, skipping");
        entry.Level.Should().Be(LogLevel.Information);
        entry.GetStateValue("Mrn").Should().Be("MRN123");
    }

    [Fact]
    public async Task Process_WithNewPollingItem_LogsInsertedMessage()
    {
        var logger = new CollectingLogger<PollingService>();
        var mockPollingItemCollection = new Mock<IMongoCollectionSet<PollingItem>>();
        var contextMock = new Mock<IMongoContext>();
        contextMock.Setup(x => x.PollingItems).Returns(mockPollingItemCollection.Object);
        mockPollingItemCollection
            .Setup(x =>
                x.FindOneAndUpdate(
                    It.IsAny<FilterDefinition<PollingItem>>(),
                    It.IsAny<UpdateDefinition<PollingItem>>(),
                    It.IsAny<FindOneAndUpdateOptions<PollingItem>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((PollingItem?)null);

        var service = new PollingService(
            logger,
            contextMock.Object,
            _mockGvmsApiClient.Object,
            _mockMatchedGmrsProducer.Object,
            _mockCompletionService.Object,
            _options,
            new PollingMetrics(_meterFactory.Object),
            _mockStorageService.Object,
            _mockTimeProvider
        );

        await service.Process(new PollingRequest { Mrn = "mrn123" }, CancellationToken.None);

        logger.Entries.Should().ContainSingle();
        var entry = logger.Entries.Single();
        entry.LogMessage.Should().Be("Inserted new polling item for {Mrn}");
        entry.Level.Should().Be(LogLevel.Information);
        entry.GetStateValue("Mrn").Should().Be("MRN123");
    }

    [Fact]
    public async Task PollItems_QueriesIncompleteItems_ByOldestFirst()
    {
        Expression<Func<PollingItem, bool>>? where = null;
        Expression<Func<PollingItem, DateTime>>? orderBy = null;
        int? limit = null;

        var mockPollingItemCollection = new Mock<IMongoCollectionSet<PollingItem>>();
        var contextMock = new Mock<IMongoContext>();
        contextMock.Setup(x => x.PollingItems).Returns(mockPollingItemCollection.Object);
        mockPollingItemCollection
            .Setup(x =>
                x.FindMany(
                    It.IsAny<Expression<Func<PollingItem, bool>>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Expression<Func<PollingItem, DateTime>>>(),
                    It.IsAny<int>()
                )
            )
            .Callback(
                (
                    Expression<Func<PollingItem, bool>> w,
                    CancellationToken _,
                    Expression<Func<PollingItem, DateTime>> o,
                    int? l
                ) =>
                {
                    where = w;
                    orderBy = o;
                    limit = l;
                }
            )
            .ReturnsAsync([]);

        var service = new PollingService(
            Mock.Of<ILogger<PollingService>>(),
            contextMock.Object,
            _mockGvmsApiClient.Object,
            _mockMatchedGmrsProducer.Object,
            _mockCompletionService.Object,
            _options,
            new PollingMetrics(_meterFactory.Object),
            _mockStorageService.Object,
            _mockTimeProvider
        );
        await service.PollItems(CancellationToken.None);

        where.Should().NotBeNull();
        where!.Compile().Invoke(new PollingItem { Complete = true }).Should().BeFalse();
        where.Compile().Invoke(new PollingItem { Complete = false }).Should().BeTrue();

        orderBy.Should().NotBeNull();
        orderBy!.Compile().Invoke(new PollingItem { LastPolled = null }).Should().Be(DateTime.MinValue);

        limit.Should().Be(_options.Value.MaxPollSize);
    }

    [Fact]
    public async Task PollItems_WithNoMRNsToPoll_DoesNothing()
    {
        var mockPollingItemCollection = new Mock<IMongoCollectionSet<PollingItem>>();
        var contextMock = new Mock<IMongoContext>();
        contextMock.Setup(x => x.PollingItems).Returns(mockPollingItemCollection.Object);
        mockPollingItemCollection
            .Setup(x =>
                x.FindMany(
                    It.IsAny<Expression<Func<PollingItem, bool>>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Expression<Func<PollingItem, DateTime>>>(),
                    It.IsAny<int>()
                )
            )
            .ReturnsAsync([]);

        var service = new PollingService(
            Mock.Of<ILogger<PollingService>>(),
            contextMock.Object,
            _mockGvmsApiClient.Object,
            _mockMatchedGmrsProducer.Object,
            _mockCompletionService.Object,
            _options,
            new PollingMetrics(_meterFactory.Object),
            _mockStorageService.Object,
            _mockTimeProvider
        );
        await service.PollItems(CancellationToken.None);

        _mockGvmsApiClient.Verify(
            x => x.SearchForGmrsByMrn(It.IsAny<MrnSearchRequest>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        mockPollingItemCollection.Verify(
            x => x.BulkWrite(It.IsAny<List<WriteModel<PollingItem>>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task PollItems_WithNoMRNsToPoll_LogsExpectedMessage()
    {
        var logger = new CollectingLogger<PollingService>();
        var mockPollingItemCollection = new Mock<IMongoCollectionSet<PollingItem>>();
        var contextMock = new Mock<IMongoContext>();
        contextMock.Setup(x => x.PollingItems).Returns(mockPollingItemCollection.Object);
        mockPollingItemCollection
            .Setup(x =>
                x.FindMany(
                    It.IsAny<Expression<Func<PollingItem, bool>>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Expression<Func<PollingItem, DateTime>>>(),
                    It.IsAny<int>()
                )
            )
            .ReturnsAsync([]);

        var service = new PollingService(
            logger,
            contextMock.Object,
            _mockGvmsApiClient.Object,
            _mockMatchedGmrsProducer.Object,
            _mockCompletionService.Object,
            _options,
            new PollingMetrics(_meterFactory.Object),
            _mockStorageService.Object,
            _mockTimeProvider
        );

        await service.PollItems(CancellationToken.None);

        logger.Entries.Should().ContainSingle();
        var entry = logger.Entries.Single();
        entry.LogMessage.Should().Be("No MRNs to poll for");
        entry.Level.Should().Be(LogLevel.Information);
    }

    [Fact]
    public async Task PollItems_WithNoResultsFromGVMS_OnlyUpdatesTheLastPolledTime()
    {
        var mockPollingItemCollection = new Mock<IMongoCollectionSet<PollingItem>>();
        var contextMock = new Mock<IMongoContext>();
        contextMock.Setup(x => x.PollingItems).Returns(mockPollingItemCollection.Object);
        mockPollingItemCollection
            .Setup(x =>
                x.FindMany(
                    It.IsAny<Expression<Func<PollingItem, bool>>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Expression<Func<PollingItem, DateTime>>>(),
                    It.IsAny<int>()
                )
            )
            .ReturnsAsync([new PollingItem { Id = "mrn123" }, new PollingItem { Id = "mrn456" }]);

        _mockGvmsApiClient
            .Setup(x => x.SearchForGmrsByMrn(It.IsAny<MrnSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new HttpResponseContent<GvmsResponse>(new GvmsResponse { GmrByDeclarationId = [], Gmrs = [] }, "{}")
            );

        List<WriteModel<PollingItem>>? writeOperations = null;
        mockPollingItemCollection
            .Setup(x => x.BulkWrite(It.IsAny<List<WriteModel<PollingItem>>>(), It.IsAny<CancellationToken>()))
            .Callback<List<WriteModel<PollingItem>>, CancellationToken>((operations, _) => writeOperations = operations)
            .Returns(Task.CompletedTask);

        _mockCompletionService
            .Setup(x => x.DetermineCompletion(It.IsAny<PollingItem>(), It.IsAny<List<Gmr>>()))
            .Returns(CompletionResult.Incomplete());

        var service = new PollingService(
            Mock.Of<ILogger<PollingService>>(),
            contextMock.Object,
            _mockGvmsApiClient.Object,
            _mockMatchedGmrsProducer.Object,
            _mockCompletionService.Object,
            _options,
            new PollingMetrics(_meterFactory.Object),
            _mockStorageService.Object,
            _mockTimeProvider
        );
        await service.PollItems(CancellationToken.None);

        var expectedDeclarationIds = new[] { "mrn123", "mrn456" };

        _mockGvmsApiClient.Verify(
            x =>
                x.SearchForGmrsByMrn(
                    It.Is<MrnSearchRequest>(p => p.DeclarationIds.SequenceEqual(expectedDeclarationIds)),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );

        var renderArgs = new RenderArgs<PollingItem>(
            BsonSerializer.SerializerRegistry.GetSerializer<PollingItem>(),
            BsonSerializer.SerializerRegistry
        );

        writeOperations!.Count.Should().Be(2);

        foreach (var updateModel in writeOperations!.OfType<UpdateOneModel<PollingItem>>())
        {
            var updateDoc = updateModel.Update.Render(renderArgs);
            var setDoc = updateDoc["$set"].AsBsonDocument;

            setDoc.Contains("LastPolled").Should().BeTrue();
            setDoc["LastPolled"].ToUniversalTime().Should().Be(_mockTimeProvider.GetUtcNow().UtcDateTime);
        }
    }

    [Fact]
    public async Task PollItems_WithNoResultsFromGvms_ClearsExistingGmrs()
    {
        var mockPollingItemCollection = new Mock<IMongoCollectionSet<PollingItem>>();
        var contextMock = new Mock<IMongoContext>();
        contextMock.Setup(x => x.PollingItems).Returns(mockPollingItemCollection.Object);
        mockPollingItemCollection
            .Setup(x =>
                x.FindMany(
                    It.IsAny<Expression<Func<PollingItem, bool>>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Expression<Func<PollingItem, DateTime>>>(),
                    It.IsAny<int>()
                )
            )
            .ReturnsAsync([
                new PollingItem
                {
                    Id = "mrn123",
                    Gmrs = new Dictionary<string, string>
                    {
                        ["gmr123"] = JsonSerializer.Serialize(
                            new Gmr
                            {
                                GmrId = "gmr123",
                                HaulierEori = "GB123",
                                State = "Submitted",
                                InspectionRequired = true,
                                UpdatedDateTime = "2025-01-01T00:00:00.0000000Z",
                                Direction = "Inbound",
                            }
                        ),
                    },
                },
            ]);

        _mockGvmsApiClient
            .Setup(x => x.SearchForGmrsByMrn(It.IsAny<MrnSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new HttpResponseContent<GvmsResponse>(new GvmsResponse { GmrByDeclarationId = [], Gmrs = [] }, "{}")
            );

        List<WriteModel<PollingItem>>? writeOperations = null;
        mockPollingItemCollection
            .Setup(x => x.BulkWrite(It.IsAny<List<WriteModel<PollingItem>>>(), It.IsAny<CancellationToken>()))
            .Callback<List<WriteModel<PollingItem>>, CancellationToken>((operations, _) => writeOperations = operations)
            .Returns(Task.CompletedTask);

        _mockCompletionService
            .Setup(x => x.DetermineCompletion(It.IsAny<PollingItem>(), It.IsAny<List<Gmr>>()))
            .Returns(CompletionResult.Incomplete());

        var service = new PollingService(
            Mock.Of<ILogger<PollingService>>(),
            contextMock.Object,
            _mockGvmsApiClient.Object,
            _mockMatchedGmrsProducer.Object,
            _mockCompletionService.Object,
            _options,
            new PollingMetrics(_meterFactory.Object),
            _mockStorageService.Object,
            _mockTimeProvider
        );

        await service.PollItems(CancellationToken.None);

        var renderArgs = new RenderArgs<PollingItem>(
            BsonSerializer.SerializerRegistry.GetSerializer<PollingItem>(),
            BsonSerializer.SerializerRegistry
        );

        var updateModel = writeOperations!.OfType<UpdateOneModel<PollingItem>>().Single();
        var updateDoc = updateModel.Update.Render(renderArgs);
        var setDoc = updateDoc["$set"].AsBsonDocument;

        setDoc.Contains("Gmrs").Should().BeTrue();
        setDoc["Gmrs"].AsBsonDocument.ElementCount.Should().Be(0);
    }

    [Fact]
    public async Task PollItems_WithResultsFromGVMS_UpdatesTheRecordsInTheDatabase()
    {
        var pollingItems = new List<PollingItem>
        {
            new() { Id = "mrn123" },
            new() { Id = "mrn456" },
            new() { Id = "mrn789" }, // No results returned for this
        };

        var mockPollingItemCollection = new Mock<IMongoCollectionSet<PollingItem>>();
        var contextMock = new Mock<IMongoContext>();
        contextMock.Setup(x => x.PollingItems).Returns(mockPollingItemCollection.Object);
        mockPollingItemCollection
            .Setup(x =>
                x.FindMany(
                    It.IsAny<Expression<Func<PollingItem, bool>>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Expression<Func<PollingItem, DateTime>>>(),
                    It.IsAny<int>()
                )
            )
            .ReturnsAsync(pollingItems);

        var gmrForMrn123 = new Gmr
        {
            GmrId = "gmr123",
            HaulierEori = "GB123",
            State = "Submitted",
            InspectionRequired = true,
            UpdatedDateTime = DateTime.UtcNow.ToString("O"),
            Direction = "Inbound",
        };

        var gmrForMrn456 = new Gmr
        {
            GmrId = "gmr456",
            HaulierEori = "GB456",
            State = "Embarked",
            InspectionRequired = false,
            UpdatedDateTime = DateTime.UtcNow.ToString("O"),
            Direction = "Outbound",
        };

        var gmrForMrn456_2 = new Gmr
        {
            GmrId = "gmr456_2",
            HaulierEori = "GB456",
            State = "Embarked",
            InspectionRequired = false,
            UpdatedDateTime = DateTime.UtcNow.ToString("O"),
            Direction = "Outbound",
        };

        var expectedGmrUpdates = new Dictionary<string, Dictionary<string, string>?>
        {
            ["mrn123"] = new() { { gmrForMrn123.GmrId, JsonSerializer.Serialize(gmrForMrn123) } },
            ["mrn456"] = new()
            {
                { gmrForMrn456.GmrId, JsonSerializer.Serialize(gmrForMrn456) },
                { gmrForMrn456_2.GmrId, JsonSerializer.Serialize(gmrForMrn456_2) },
            },
            ["mrn789"] = new(),
        };

        _mockGvmsApiClient
            .Setup(x => x.SearchForGmrsByMrn(It.IsAny<MrnSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new HttpResponseContent<GvmsResponse>(
                    new GvmsResponse
                    {
                        GmrByDeclarationId =
                        [
                            new GmrDeclaration { dec = "mrn123", gmrs = ["gmr123"] },
                            new GmrDeclaration { dec = "mrn456", gmrs = ["gmr456", "gmr456_2"] },
                        ],
                        Gmrs = [gmrForMrn123, gmrForMrn456, gmrForMrn456_2],
                    },
                    "{}"
                )
            );

        List<WriteModel<PollingItem>>? writeOperations = null;
        mockPollingItemCollection
            .Setup(x => x.BulkWrite(It.IsAny<List<WriteModel<PollingItem>>>(), It.IsAny<CancellationToken>()))
            .Callback<List<WriteModel<PollingItem>>, CancellationToken>((operations, _) => writeOperations = operations)
            .Returns(Task.CompletedTask);

        _mockCompletionService
            .Setup(x => x.DetermineCompletion(It.IsAny<PollingItem>(), It.IsAny<List<Gmr>>()))
            .Returns(CompletionResult.Incomplete());

        var service = new PollingService(
            Mock.Of<ILogger<PollingService>>(),
            contextMock.Object,
            _mockGvmsApiClient.Object,
            _mockMatchedGmrsProducer.Object,
            _mockCompletionService.Object,
            _options,
            new PollingMetrics(_meterFactory.Object),
            _mockStorageService.Object,
            _mockTimeProvider
        );
        await service.PollItems(CancellationToken.None);

        var renderArgs = new RenderArgs<PollingItem>(
            BsonSerializer.SerializerRegistry.GetSerializer<PollingItem>(),
            BsonSerializer.SerializerRegistry
        );

        foreach (var (mrn, expectedGmrs) in expectedGmrUpdates)
        {
            var updateModel = writeOperations!
                .OfType<UpdateOneModel<PollingItem>>()
                .Single(model =>
                {
                    var filterDoc = model.Filter.Render(renderArgs);
                    return filterDoc["_id"].AsString == mrn;
                });

            var updateDoc = updateModel.Update.Render(renderArgs);
            var setDoc = updateDoc["$set"].AsBsonDocument;

            setDoc.Contains("LastPolled").Should().BeTrue();
            setDoc["LastPolled"].ToUniversalTime().Should().Be(_mockTimeProvider.GetUtcNow().UtcDateTime);

            setDoc.Contains("Gmrs").Should().BeTrue();
            var deserialisedGmrs = BsonSerializer.Deserialize<Dictionary<string, string>>(
                setDoc["Gmrs"].AsBsonDocument
            );
            deserialisedGmrs.Should().BeEquivalentTo(expectedGmrs);
        }
    }

    [Fact]
    public async Task PollItems_WithResultsFromGvms_LogsExpectedMessages_WhenNoGmrsChanged()
    {
        var logger = new CollectingLogger<PollingService>();

        var updatedDateTime = "2025-01-01T00:00:00.0000000Z";
        var gmrForMrn123 = new Gmr
        {
            GmrId = "gmr123",
            HaulierEori = "GB123",
            State = "Submitted",
            InspectionRequired = true,
            UpdatedDateTime = updatedDateTime,
            Direction = "Inbound",
        };
        var gmrForMrn456 = new Gmr
        {
            GmrId = "gmr456",
            HaulierEori = "GB456",
            State = "Embarked",
            InspectionRequired = false,
            UpdatedDateTime = updatedDateTime,
            Direction = "Outbound",
        };

        var pollingItems = new List<PollingItem>
        {
            new()
            {
                Id = "mrn123",
                Gmrs = new Dictionary<string, string> { [gmrForMrn123.GmrId] = JsonSerializer.Serialize(gmrForMrn123) },
            },
            new()
            {
                Id = "mrn456",
                Gmrs = new Dictionary<string, string> { [gmrForMrn456.GmrId] = JsonSerializer.Serialize(gmrForMrn456) },
            },
        };

        var mockPollingItemCollection = new Mock<IMongoCollectionSet<PollingItem>>();
        var contextMock = new Mock<IMongoContext>();
        contextMock.Setup(x => x.PollingItems).Returns(mockPollingItemCollection.Object);
        mockPollingItemCollection
            .Setup(x =>
                x.FindMany(
                    It.IsAny<Expression<Func<PollingItem, bool>>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Expression<Func<PollingItem, DateTime>>>(),
                    It.IsAny<int>()
                )
            )
            .ReturnsAsync(pollingItems);

        _mockGvmsApiClient
            .Setup(x => x.SearchForGmrsByMrn(It.IsAny<MrnSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new HttpResponseContent<GvmsResponse>(
                    new GvmsResponse
                    {
                        GmrByDeclarationId =
                        [
                            new GmrDeclaration { dec = "mrn123", gmrs = ["gmr123"] },
                            new GmrDeclaration { dec = "mrn456", gmrs = ["gmr456"] },
                        ],
                        Gmrs = [gmrForMrn123, gmrForMrn456],
                    },
                    "{}"
                )
            );

        mockPollingItemCollection
            .Setup(x => x.BulkWrite(It.IsAny<List<WriteModel<PollingItem>>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockCompletionService
            .Setup(x => x.DetermineCompletion(It.IsAny<PollingItem>(), It.IsAny<List<Gmr>>()))
            .Returns(CompletionResult.Incomplete());

        var service = new PollingService(
            logger,
            contextMock.Object,
            _mockGvmsApiClient.Object,
            _mockMatchedGmrsProducer.Object,
            _mockCompletionService.Object,
            _options,
            new PollingMetrics(_meterFactory.Object),
            _mockStorageService.Object,
            _mockTimeProvider
        );

        await service.PollItems(CancellationToken.None);

        logger
            .Entries.Select(entry => entry.LogMessage)
            .Should()
            .Equal(
                "Polling GVMS for 2 MRNs: mrn123,mrn456",
                "GVMS poll completed in {ElapsedMs} ms",
                "GVMS response: Found {MatchedMrnCount} MRNs with GMRs, {UnmatchedMrnCount} without, {GmrCount} unique GMRs",
                "Updated {UpdatedCount} polling items, {ItemsWithGmrs} had GMRs, {CompletedCount} marked complete, {UpdatesMade} updates made",
                "No changed GMRs to publish for polled MRNs"
            );

        var pollEntry = logger.Entries[0];
        pollEntry.State.Count.Should().Be(1);

        var responseEntry = logger.Entries[2];
        responseEntry.GetStateValue("MatchedMrnCount").Should().Be(2);
        responseEntry.GetStateValue("UnmatchedMrnCount").Should().Be(0);
        responseEntry.GetStateValue("GmrCount").Should().Be(2);

        var updatedEntry = logger.Entries[3];
        updatedEntry.GetStateValue("UpdatedCount").Should().Be(2);
        updatedEntry.GetStateValue("ItemsWithGmrs").Should().Be(2);
        updatedEntry.GetStateValue("CompletedCount").Should().Be(0);
        updatedEntry.GetStateValue("UpdatesMade").Should().Be(2);
    }

    [Fact]
    public async Task PollItems_WithResultsFromGvms_LogsExpectedMessages_WhenPublishingChanges()
    {
        var logger = new CollectingLogger<PollingService>();
        var pollingItems = new List<PollingItem> { new() { Id = "mrn123" } };

        var mockPollingItemCollection = new Mock<IMongoCollectionSet<PollingItem>>();
        var contextMock = new Mock<IMongoContext>();
        contextMock.Setup(x => x.PollingItems).Returns(mockPollingItemCollection.Object);
        mockPollingItemCollection
            .Setup(x =>
                x.FindMany(
                    It.IsAny<Expression<Func<PollingItem, bool>>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Expression<Func<PollingItem, DateTime>>>(),
                    It.IsAny<int>()
                )
            )
            .ReturnsAsync(pollingItems);

        var gmrForMrn123 = new Gmr
        {
            GmrId = "gmr123",
            HaulierEori = "GB123",
            State = "Submitted",
            InspectionRequired = true,
            UpdatedDateTime = "2025-01-01T00:00:00.0000000Z",
            Direction = "Inbound",
        };

        _mockGvmsApiClient
            .Setup(x => x.SearchForGmrsByMrn(It.IsAny<MrnSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new HttpResponseContent<GvmsResponse>(
                    new GvmsResponse
                    {
                        GmrByDeclarationId = [new GmrDeclaration { dec = "mrn123", gmrs = ["gmr123"] }],
                        Gmrs = [gmrForMrn123],
                    },
                    "{}"
                )
            );

        mockPollingItemCollection
            .Setup(x => x.BulkWrite(It.IsAny<List<WriteModel<PollingItem>>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockCompletionService
            .Setup(x => x.DetermineCompletion(It.IsAny<PollingItem>(), It.IsAny<List<Gmr>>()))
            .Returns(CompletionResult.Incomplete());

        var service = new PollingService(
            logger,
            contextMock.Object,
            _mockGvmsApiClient.Object,
            _mockMatchedGmrsProducer.Object,
            _mockCompletionService.Object,
            _options,
            new PollingMetrics(_meterFactory.Object),
            _mockStorageService.Object,
            _mockTimeProvider
        );

        await service.PollItems(CancellationToken.None);

        logger
            .Entries.Select(entry => entry.LogMessage)
            .Should()
            .Equal(
                "Polling GVMS for 1 MRNs: mrn123",
                "GVMS poll completed in {ElapsedMs} ms",
                "GVMS response: Found {MatchedMrnCount} MRNs with GMRs, {UnmatchedMrnCount} without, {GmrCount} unique GMRs",
                "Updated {UpdatedCount} polling items, {ItemsWithGmrs} had GMRs, {CompletedCount} marked complete, {UpdatesMade} updates made",
                "Published {MatchedCount} changed GMRs"
            );

        var responseEntry = logger.Entries[2];
        responseEntry.GetStateValue("MatchedMrnCount").Should().Be(1);
        responseEntry.GetStateValue("UnmatchedMrnCount").Should().Be(0);
        responseEntry.GetStateValue("GmrCount").Should().Be(1);

        var updatedEntry = logger.Entries[3];
        updatedEntry.GetStateValue("UpdatedCount").Should().Be(1);
        updatedEntry.GetStateValue("ItemsWithGmrs").Should().Be(1);
        updatedEntry.GetStateValue("CompletedCount").Should().Be(0);
        updatedEntry.GetStateValue("UpdatesMade").Should().Be(1);

        var publishedEntry = logger.Entries[4];
        publishedEntry.GetStateValue("MatchedCount").Should().Be(1);
    }

    [Fact]
    public async Task PollItems_WithMatchedGmrs_PublishesTheResults()
    {
        var gmrForMrn123 = new Gmr
        {
            GmrId = "gmr123",
            HaulierEori = "GB123",
            State = "Submitted",
            InspectionRequired = true,
            UpdatedDateTime = DateTime.UtcNow.ToString("O"),
            Direction = "Inbound",
        };

        var gmrForMrn456 = new Gmr
        {
            GmrId = "gmr456",
            HaulierEori = "GB456",
            State = "Embarked",
            InspectionRequired = false,
            UpdatedDateTime = DateTime.UtcNow.ToString("O"),
            Direction = "Outbound",
        };

        var gmrForMrn456_2 = new Gmr
        {
            GmrId = "gmr456_2",
            HaulierEori = "GB456",
            State = "Embarked",
            InspectionRequired = false,
            UpdatedDateTime = DateTime.UtcNow.ToString("O"),
            Direction = "Outbound",
        };

        var pollingItems = new List<PollingItem>
        {
            new() { Id = "mrn123" },
            new() { Id = "mrn456" },
            new() { Id = "mrn789" }, // No results returned for this
            new() { Id = "mrnNoChanges", Gmrs = { { gmrForMrn123.GmrId, JsonSerializer.Serialize(gmrForMrn123) } } },
        };

        var mockPollingItemCollection = new Mock<IMongoCollectionSet<PollingItem>>();
        var contextMock = new Mock<IMongoContext>();
        contextMock.Setup(x => x.PollingItems).Returns(mockPollingItemCollection.Object);
        mockPollingItemCollection
            .Setup(x =>
                x.FindMany(
                    It.IsAny<Expression<Func<PollingItem, bool>>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Expression<Func<PollingItem, DateTime>>>(),
                    It.IsAny<int>()
                )
            )
            .ReturnsAsync(pollingItems);

        var expectedMatchedGmrs = new List<MatchedGmr>
        {
            new() { Mrn = "mrn123", Gmr = gmrForMrn123 },
            new() { Mrn = "mrn456", Gmr = gmrForMrn456 },
            new() { Mrn = "mrn456", Gmr = gmrForMrn456_2 },
        };

        _mockGvmsApiClient
            .Setup(x => x.SearchForGmrsByMrn(It.IsAny<MrnSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new HttpResponseContent<GvmsResponse>(
                    new GvmsResponse
                    {
                        GmrByDeclarationId =
                        [
                            new GmrDeclaration { dec = "mrn123", gmrs = ["gmr123"] },
                            new GmrDeclaration { dec = "mrn456", gmrs = ["gmr456", "gmr456_2"] },
                            new GmrDeclaration { dec = "mrnNoChanges", gmrs = ["gmr123"] },
                        ],
                        Gmrs = [gmrForMrn123, gmrForMrn456, gmrForMrn456_2],
                    },
                    "{}"
                )
            );

        List<MatchedGmr>? matchedGmrs = null;
        _mockMatchedGmrsProducer
            .Setup(x => x.PublishMatchedGmrs(It.IsAny<List<MatchedGmr>>(), It.IsAny<CancellationToken>()))
            .Callback<List<MatchedGmr>, CancellationToken>((records, _) => matchedGmrs = records);

        _mockCompletionService
            .Setup(x => x.DetermineCompletion(It.IsAny<PollingItem>(), It.IsAny<List<Gmr>>()))
            .Returns(CompletionResult.Incomplete());

        var service = new PollingService(
            Mock.Of<ILogger<PollingService>>(),
            contextMock.Object,
            _mockGvmsApiClient.Object,
            _mockMatchedGmrsProducer.Object,
            _mockCompletionService.Object,
            _options,
            new PollingMetrics(_meterFactory.Object),
            _mockStorageService.Object,
            _mockTimeProvider
        );

        await service.PollItems(CancellationToken.None);

        matchedGmrs!.Should().BeEquivalentTo(expectedMatchedGmrs);
        matchedGmrs!.Should().NotContain(p => p.Mrn == "mrnNoChanges");
    }

    [Fact]
    public async Task PollItems_WhenCompletionServiceReturnsTrue_SetsCompleteToTrue()
    {
        var pollingItems = new List<PollingItem>
        {
            new() { Id = "mrn123" },
            new() { Id = "mrn456" },
        };

        var mockPollingItemCollection = new Mock<IMongoCollectionSet<PollingItem>>();
        var contextMock = new Mock<IMongoContext>();
        contextMock.Setup(x => x.PollingItems).Returns(mockPollingItemCollection.Object);
        mockPollingItemCollection
            .Setup(x =>
                x.FindMany(
                    It.IsAny<Expression<Func<PollingItem, bool>>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Expression<Func<PollingItem, DateTime>>>(),
                    It.IsAny<int>()
                )
            )
            .ReturnsAsync(pollingItems);

        var gmrForMrn123 = new Gmr
        {
            GmrId = "gmr123",
            HaulierEori = "GB123",
            State = "COMPLETED",
            UpdatedDateTime = DateTime.UtcNow.ToString("O"),
            Direction = "Inbound",
        };

        _mockGvmsApiClient
            .Setup(x => x.SearchForGmrsByMrn(It.IsAny<MrnSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new HttpResponseContent<GvmsResponse>(
                    new GvmsResponse
                    {
                        GmrByDeclarationId = [new GmrDeclaration { dec = "mrn123", gmrs = ["gmr123"] }],
                        Gmrs = [gmrForMrn123],
                    },
                    "{}"
                )
            );

        // Mock completion service to return true for mrn123, false for mrn456
        _mockCompletionService
            .Setup(x => x.DetermineCompletion(It.Is<PollingItem>(p => p.Id == "mrn123"), It.IsAny<List<Gmr>>()))
            .Returns(CompletionResult.Complete(CompletionReason.Complete, new TimeSpan(1, 0, 0)));

        _mockCompletionService
            .Setup(x => x.DetermineCompletion(It.Is<PollingItem>(p => p.Id == "mrn456"), It.IsAny<List<Gmr>>()))
            .Returns(CompletionResult.Incomplete());

        List<WriteModel<PollingItem>>? writeOperations = null;
        mockPollingItemCollection
            .Setup(x => x.BulkWrite(It.IsAny<List<WriteModel<PollingItem>>>(), It.IsAny<CancellationToken>()))
            .Callback<List<WriteModel<PollingItem>>, CancellationToken>((operations, _) => writeOperations = operations)
            .Returns(Task.CompletedTask);

        var service = new PollingService(
            Mock.Of<ILogger<PollingService>>(),
            contextMock.Object,
            _mockGvmsApiClient.Object,
            _mockMatchedGmrsProducer.Object,
            _mockCompletionService.Object,
            _options,
            new PollingMetrics(_meterFactory.Object),
            _mockStorageService.Object,
            _mockTimeProvider
        );

        await service.PollItems(CancellationToken.None);

        var renderArgs = new RenderArgs<PollingItem>(
            BsonSerializer.SerializerRegistry.GetSerializer<PollingItem>(),
            BsonSerializer.SerializerRegistry
        );

        // Verify mrn123 has Complete set to true
        var mrn123Update = writeOperations!
            .OfType<UpdateOneModel<PollingItem>>()
            .Single(model =>
            {
                var filterDoc = model.Filter.Render(renderArgs);
                return filterDoc["_id"].AsString == "mrn123";
            });

        var updateDoc123 = mrn123Update.Update.Render(renderArgs);
        var setDoc123 = updateDoc123["$set"].AsBsonDocument;
        setDoc123.Contains("Complete").Should().BeTrue();
        setDoc123["Complete"].AsBoolean.Should().BeTrue();

        // Verify mrn456 does not have Complete set
        var mrn456Update = writeOperations!
            .OfType<UpdateOneModel<PollingItem>>()
            .Single(model =>
            {
                var filterDoc = model.Filter.Render(renderArgs);
                return filterDoc["_id"].AsString == "mrn456";
            });

        var updateDoc456 = mrn456Update.Update.Render(renderArgs);
        var setDoc456 = updateDoc456["$set"].AsBsonDocument;
        setDoc456.Contains("Complete").Should().BeFalse();
    }

    [Fact]
    public async Task PollItems_CallsCompletionServiceWithCorrectGmrs()
    {
        var pollingItems = new List<PollingItem> { new() { Id = "mrn123" } };

        var mockPollingItemCollection = new Mock<IMongoCollectionSet<PollingItem>>();
        var contextMock = new Mock<IMongoContext>();
        contextMock.Setup(x => x.PollingItems).Returns(mockPollingItemCollection.Object);
        mockPollingItemCollection
            .Setup(x =>
                x.FindMany(
                    It.IsAny<Expression<Func<PollingItem, bool>>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Expression<Func<PollingItem, DateTime>>>(),
                    It.IsAny<int>()
                )
            )
            .ReturnsAsync(pollingItems);

        var gmrForMrn123 = new Gmr
        {
            GmrId = "gmr123",
            HaulierEori = "GB123",
            State = "Submitted",
            UpdatedDateTime = DateTime.UtcNow.ToString("O"),
            Direction = "Inbound",
        };

        _mockGvmsApiClient
            .Setup(x => x.SearchForGmrsByMrn(It.IsAny<MrnSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new HttpResponseContent<GvmsResponse>(
                    new GvmsResponse
                    {
                        GmrByDeclarationId = [new GmrDeclaration { dec = "mrn123", gmrs = ["gmr123"] }],
                        Gmrs = [gmrForMrn123],
                    },
                    "{}"
                )
            );

        _mockCompletionService
            .Setup(x => x.DetermineCompletion(It.IsAny<PollingItem>(), It.IsAny<List<Gmr>>()))
            .Returns(CompletionResult.Incomplete());

        var service = new PollingService(
            Mock.Of<ILogger<PollingService>>(),
            contextMock.Object,
            _mockGvmsApiClient.Object,
            _mockMatchedGmrsProducer.Object,
            _mockCompletionService.Object,
            _options,
            new PollingMetrics(_meterFactory.Object),
            _mockStorageService.Object,
            _mockTimeProvider
        );

        await service.PollItems(CancellationToken.None);

        _mockCompletionService.Verify(
            x =>
                x.DetermineCompletion(
                    It.Is<PollingItem>(p => p.Id == "mrn123"),
                    It.Is<List<Gmr>>(gmrs => gmrs.Count == 1 && gmrs[0].GmrId == "gmr123")
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task PollItems_CallsCompletionServiceWithEmptyGmrsWhenNoMatches()
    {
        var pollingItems = new List<PollingItem> { new() { Id = "mrn123" } };

        var mockPollingItemCollection = new Mock<IMongoCollectionSet<PollingItem>>();
        var contextMock = new Mock<IMongoContext>();
        contextMock.Setup(x => x.PollingItems).Returns(mockPollingItemCollection.Object);
        mockPollingItemCollection
            .Setup(x =>
                x.FindMany(
                    It.IsAny<Expression<Func<PollingItem, bool>>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Expression<Func<PollingItem, DateTime>>>(),
                    It.IsAny<int>()
                )
            )
            .ReturnsAsync(pollingItems);

        _mockGvmsApiClient
            .Setup(x => x.SearchForGmrsByMrn(It.IsAny<MrnSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new HttpResponseContent<GvmsResponse>(new GvmsResponse { GmrByDeclarationId = [], Gmrs = [] }, "{}")
            );

        _mockCompletionService
            .Setup(x => x.DetermineCompletion(It.IsAny<PollingItem>(), It.IsAny<List<Gmr>>()))
            .Returns(CompletionResult.Incomplete());

        var service = new PollingService(
            Mock.Of<ILogger<PollingService>>(),
            contextMock.Object,
            _mockGvmsApiClient.Object,
            _mockMatchedGmrsProducer.Object,
            _mockCompletionService.Object,
            _options,
            new PollingMetrics(_meterFactory.Object),
            _mockStorageService.Object,
            _mockTimeProvider
        );

        await service.PollItems(CancellationToken.None);

        _mockCompletionService.Verify(
            x =>
                x.DetermineCompletion(
                    It.Is<PollingItem>(p => p.Id == "mrn123"),
                    It.Is<List<Gmr>>(gmrs => gmrs.Count == 0)
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task PollItems_CallsTryStoreSearchResultsAsync_WithGvmsResponse()
    {
        const string expectedResponseContent = "{\"gmrs\":[]}";
        var pollingItems = new List<PollingItem> { new() { Id = "mrn123" } };

        var mockPollingItemCollection = new Mock<IMongoCollectionSet<PollingItem>>();
        var contextMock = new Mock<IMongoContext>();
        contextMock.Setup(x => x.PollingItems).Returns(mockPollingItemCollection.Object);
        mockPollingItemCollection
            .Setup(x =>
                x.FindMany(
                    It.IsAny<Expression<Func<PollingItem, bool>>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Expression<Func<PollingItem, DateTime>>>(),
                    It.IsAny<int>()
                )
            )
            .ReturnsAsync(pollingItems);

        _mockGvmsApiClient
            .Setup(x => x.SearchForGmrsByMrn(It.IsAny<MrnSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new HttpResponseContent<GvmsResponse>(
                    new GvmsResponse { GmrByDeclarationId = [], Gmrs = [] },
                    expectedResponseContent
                )
            );

        _mockCompletionService
            .Setup(x => x.DetermineCompletion(It.IsAny<PollingItem>(), It.IsAny<List<Gmr>>()))
            .Returns(CompletionResult.Incomplete());

        var service = new PollingService(
            Mock.Of<ILogger<PollingService>>(),
            contextMock.Object,
            _mockGvmsApiClient.Object,
            _mockMatchedGmrsProducer.Object,
            _mockCompletionService.Object,
            _options,
            new PollingMetrics(_meterFactory.Object),
            _mockStorageService.Object,
            _mockTimeProvider
        );

        await service.PollItems(CancellationToken.None);

        _mockStorageService.Verify(x => x.TryStoreSearchResultsAsync(expectedResponseContent), Times.Once);
    }

    private sealed class CollectingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            var stateValues = state as IReadOnlyList<KeyValuePair<string, object?>> ?? [];
            var logMessage = stateValues.FirstOrDefault(kv => kv.Key == "{OriginalFormat}").Value?.ToString();
            Entries.Add(new LogEntry(logLevel, logMessage, stateValues));
        }
    }

    private sealed record LogEntry(
        LogLevel Level,
        string? LogMessage,
        IReadOnlyList<KeyValuePair<string, object?>> State
    )
    {
        public object? GetStateValue(string key)
        {
            return State.FirstOrDefault(kv => kv.Key == key).Value;
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose() { }
    }
}
