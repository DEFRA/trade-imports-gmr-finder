using GvmsClient.Contract;
using GvmsClient.Contract.Requests;
using GvmsClient.Contract.Responses;

namespace GvmsClient.Client;

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
