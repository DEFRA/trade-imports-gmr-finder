namespace GvmsClient.Client;

public class HttpResponseContent<T>(T result, string stringResult)
{
    public T Result { get; } = result;
    public string StringResult { get; } = stringResult;
}
