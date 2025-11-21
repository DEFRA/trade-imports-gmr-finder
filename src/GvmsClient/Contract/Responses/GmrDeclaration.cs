using System.Diagnostics.CodeAnalysis;

namespace Defra.TradeImportsGmrFinder.GvmsClient.Contract.Responses;

[ExcludeFromCodeCoverage]
public class GmrDeclaration
{
    public string dec { get; set; } = string.Empty;
    public List<string> gmrs { get; set; } = new List<string>();
}
