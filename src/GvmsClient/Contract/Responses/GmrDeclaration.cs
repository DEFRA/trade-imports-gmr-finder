namespace GvmsClient.Contract.Responses;

public class GmrDeclaration
{
    public string dec { get; set; } = string.Empty;
    public List<string> gmrs { get; set; } = new List<string>();
}
