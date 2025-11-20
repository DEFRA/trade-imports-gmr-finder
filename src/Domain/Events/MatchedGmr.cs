using System.Diagnostics.CodeAnalysis;
using Defra.TradeImportsGmrFinder.GvmsClient.Contract;

namespace Defra.TradeImportsGmrFinder.Domain.Events;

[ExcludeFromCodeCoverage]
public record MatchedGmr
{
    public string? Mrn { get; init; }
    public required Gmr Gmr { get; init; }

    public string GetIdentifier => $"{Mrn ?? "Unknown"}-{Gmr.GmrId}";
};
