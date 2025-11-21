using System.Diagnostics.CodeAnalysis;

namespace Defra.TradeImportsGmrFinder.GvmsClient.Contract.Responses;

[ExcludeFromCodeCoverage]
public class GmrByVrn
{
    public string vrn { get; set; } = string.Empty;
    public List<string> gmrs { get; set; } = [];
}
