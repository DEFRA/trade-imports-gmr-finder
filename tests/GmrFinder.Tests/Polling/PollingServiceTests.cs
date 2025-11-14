using System.Linq.Expressions;
using System.Text.Json;
using Domain.Events;
using FluentAssertions;
using GmrFinder.Configuration;
using GmrFinder.Data;
using GmrFinder.Polling;
using GmrFinder.Producers;
using GvmsClient.Client;
using GvmsClient.Contract;
using GvmsClient.Contract.Requests;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Moq;

namespace GmrFinder.Tests.Polling;

public class PollingServiceTests
{
    private readonly IOptions<PollingServiceOptions> _options = Options.Create(new PollingServiceOptions());
    private readonly Mock<IGvmsApiClient> _mockGvmsApiClient = new();
    private readonly Mock<IMatchedGmrsProducer> _mockMatchedGmrsProducer = new();
    private readonly TimeProvider _mockTimeProvider = new FakeTimeProvider(
        new DateTimeOffset(2025, 11, 7, 11, 10, 15, TimeSpan.Zero)
    );

    [Fact]
    public async Task Process_PollingItemAlreadyExists_DoesNotInsert()
    {
        var existing = new PollingItem { Id = "mrn123", Created = DateTime.UtcNow };
        Expression<Func<PollingItem, bool>>? queryExpression = null;

        var mockPollingItemCollection = new Mock<IMongoCollectionSet<PollingItem>>();
        var contextMock = new Mock<IMongoContext>();
        contextMock.Setup(x => x.PollingItems).Returns(mockPollingItemCollection.Object);
        mockPollingItemCollection
            .Setup(x => x.FindOne(It.IsAny<Expression<Func<PollingItem, bool>>>(), It.IsAny<CancellationToken>()))
            .Callback<Expression<Func<PollingItem, bool>>, CancellationToken>((expr, _) => queryExpression = expr)
            .ReturnsAsync(existing);

        var service = new PollingService(
            Mock.Of<ILogger<PollingService>>(),
            contextMock.Object,
            _mockGvmsApiClient.Object,
            _mockMatchedGmrsProducer.Object,
            _options,
            _mockTimeProvider
        );
        var request = new PollingRequest { Mrn = existing.Id };

        await service.Process(request, CancellationToken.None);

        mockPollingItemCollection.Verify(
            x => x.Insert(It.IsAny<PollingItem>(), It.IsAny<CancellationToken>()),
            Times.Never
        );

        queryExpression!.Compile().Invoke(new PollingItem { Id = existing.Id }).Should().BeTrue();
    }

    [Fact]
    public async Task Process_PollingItemDoesNotExist_IsInserted()
    {
        var expectedMrn = "mrn123";

        var mockPollingItemCollection = new Mock<IMongoCollectionSet<PollingItem>>();
        var contextMock = new Mock<IMongoContext>();
        contextMock.Setup(x => x.PollingItems).Returns(mockPollingItemCollection.Object);
        mockPollingItemCollection.Setup(x => x.Insert(It.IsAny<PollingItem>(), It.IsAny<CancellationToken>()));

        var service = new PollingService(
            Mock.Of<ILogger<PollingService>>(),
            contextMock.Object,
            _mockGvmsApiClient.Object,
            _mockMatchedGmrsProducer.Object,
            _options,
            _mockTimeProvider
        );
        var request = new PollingRequest { Mrn = expectedMrn };

        await service.Process(request, CancellationToken.None);

        mockPollingItemCollection.Verify(
            x => x.Insert(It.Is<PollingItem>(p => p.Id == expectedMrn), It.IsAny<CancellationToken>()),
            Times.Once
        );
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
            _options,
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
            _options,
            _mockTimeProvider
        );
        await service.PollItems(CancellationToken.None);

        _mockGvmsApiClient.Verify(
            x => x.SearchForGmrs(It.IsAny<MrnSearchRequest>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        mockPollingItemCollection.Verify(
            x => x.BulkWrite(It.IsAny<List<WriteModel<PollingItem>>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
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
            .Setup(x => x.SearchForGmrs(It.IsAny<MrnSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new HttpResponseContent<GvmsResponse>(new GvmsResponse { GmrByDeclarationId = [], Gmrs = [] }, "{}")
            );

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
            _options,
            _mockTimeProvider
        );
        await service.PollItems(CancellationToken.None);

        var expectedDeclarationIds = new[] { "mrn123", "mrn456" };

        _mockGvmsApiClient.Verify(
            x =>
                x.SearchForGmrs(
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
            ["mrn789"] = null,
        };

        _mockGvmsApiClient
            .Setup(x => x.SearchForGmrs(It.IsAny<MrnSearchRequest>(), It.IsAny<CancellationToken>()))
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

        var service = new PollingService(
            Mock.Of<ILogger<PollingService>>(),
            contextMock.Object,
            _mockGvmsApiClient.Object,
            _mockMatchedGmrsProducer.Object,
            _options,
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

            if (expectedGmrs is null)
            {
                setDoc.Contains("Gmrs").Should().BeFalse();
                continue;
            }

            var deserialisedGmrs = BsonSerializer.Deserialize<Dictionary<string, string>>(
                setDoc["Gmrs"].AsBsonDocument
            );
            deserialisedGmrs.Should().BeEquivalentTo(expectedGmrs);
        }
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
            .Setup(x => x.SearchForGmrs(It.IsAny<MrnSearchRequest>(), It.IsAny<CancellationToken>()))
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

        var service = new PollingService(
            Mock.Of<ILogger<PollingService>>(),
            contextMock.Object,
            _mockGvmsApiClient.Object,
            _mockMatchedGmrsProducer.Object,
            _options,
            _mockTimeProvider
        );

        await service.PollItems(CancellationToken.None);

        matchedGmrs!.Should().BeEquivalentTo(expectedMatchedGmrs);
        matchedGmrs!.Should().NotContain(p => p.Mrn == "mrnNoChanges");
    }
}
