using Defra.TradeImportsGmrFinder.GvmsClient.Client;
using Defra.TradeImportsGmrFinder.GvmsClient.Contract;
using Defra.TradeImportsGmrFinder.GvmsClient.Contract.Requests;

namespace GmrFinder.Services;

public interface IGvmsApiClientService
{
    Task<HttpResponseContent<GvmsResponse>> SearchForGmrsByMrn(
        MrnSearchRequest request,
        CancellationToken cancellationToken
    );
}
