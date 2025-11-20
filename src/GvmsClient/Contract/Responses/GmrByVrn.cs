namespace Defra.TradeImportsGmrFinder.GvmsClient.Contract.Responses;

public class GmrByVrn
{
    public string vrn { get; set; } = string.Empty;
    public List<string> gmrs { get; set; } = [];
}
