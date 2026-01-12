using Defra.TradeImportsGmrFinder.Domain.Events;
using Defra.TradeImportsGmrFinder.GvmsClient.Contract;
using GmrFinder.Producers;
using Microsoft.Extensions.Logging.Abstractions;

namespace GmrFinder.Tests.Producers;

public class StubMatchedGmrsProducerTests
{
    private readonly StubMatchedGmrsProducer _producer = new(NullLogger<StubMatchedGmrsProducer>.Instance);

    [Fact]
    public async Task PublishMatchedGmrs_WithEmptyList_CompletesSuccessfully()
    {
        var matchedRecords = new List<MatchedGmr>();

        var result = async () => await _producer.PublishMatchedGmrs(matchedRecords, CancellationToken.None);

        await result.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishMatchedGmrs_WithRecords_CompletesSuccessfully()
    {
        var matchedRecords = new List<MatchedGmr>
        {
            new()
            {
                Mrn = "25GB6RLA6C8OV8GAR2",
                Gmr = new Gmr
                {
                    GmrId = "GMR-123",
                    HaulierEori = "GB123456789000",
                    State = "active",
                    UpdatedDateTime = "2026-01-12T00:00:00Z",
                    Direction = "GB_TO_NI",
                },
            },
        };

        var act = async () => await _producer.PublishMatchedGmrs(matchedRecords, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
