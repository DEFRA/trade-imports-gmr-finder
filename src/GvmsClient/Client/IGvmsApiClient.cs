using Defra.TradeImportsGmrFinder.GvmsClient.Contract;
using Defra.TradeImportsGmrFinder.GvmsClient.Contract.Requests;
using Defra.TradeImportsGmrFinder.GvmsClient.Contract.Responses;

namespace Defra.TradeImportsGmrFinder.GvmsClient.Client;

public interface IGvmsApiClient
{
    Task<HttpResponseContent<GvmsResponse>> SearchForGmrs(
        MrnSearchRequest request,
        CancellationToken cancellationToken
    );
    Task<HttpResponseContent<VrnSearchResponse>> SearchForGmrs(
        VrnSearchRequest request,
        CancellationToken cancellationToken
    );
    Task<HttpResponseContent<TrnSearchResponse>> SearchForGmrs(
        TrnSearchRequest request,
        CancellationToken cancellationToken
    );
    Task HoldGmr(string gmrId, bool holdStatus, CancellationToken cancellationToken);
}
